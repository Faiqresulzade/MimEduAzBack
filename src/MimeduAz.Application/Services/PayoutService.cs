using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Contracts.Payouts;
using MimeduAz.Domain.Entities;
using MimeduAz.Domain.Enums;

namespace MimeduAz.Application.Services;

public interface IPayoutService
{
    Task<EarningsDto> GetMyEarningsAsync(CancellationToken ct);
    Task<PayoutDto> RequestAsync(CreatePayoutRequest request, CancellationToken ct);
    Task<IReadOnlyList<AdminPayoutDto>> GetAllAsync(CancellationToken ct);
    Task<AdminPayoutDto> ProcessAsync(Guid id, ProcessPayoutRequest request, CancellationToken ct);
}

/// <summary>
/// Müəllif qazancı və onun bank köçürməsi ilə çıxarılması. Qazanc yalnız ödənilmiş (Paid)
/// sifariş sətirlərinin <c>AuthorPayoutAmount</c> cəmidir - qaytarılan sifariş özü çıxır.
/// </summary>
public sealed class PayoutService : IPayoutService
{
    public const decimal MinimumPayout = 20m;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<PayoutService> _logger;

    public PayoutService(IApplicationDbContext db, ICurrentUserService currentUser, ILogger<PayoutService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<EarningsDto> GetMyEarningsAsync(CancellationToken ct) =>
        await BuildEarningsAsync(_currentUser.RequireUserId(), ct);

    public async Task<PayoutDto> RequestAsync(CreatePayoutRequest request, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();
        var earnings = await BuildEarningsAsync(userId, ct);

        if (earnings.PendingPayout > 0)
        {
            throw new ConflictException("Əvvəlki ödəniş tələbiniz hələ icra olunmayıb.");
        }

        if (earnings.Available < MinimumPayout)
        {
            throw new BadRequestException($"Ödəniş tələbi üçün balans ən azı {MinimumPayout:0} ₼ olmalıdır.");
        }

        var payout = new PayoutRequest
        {
            UserId = userId,
            Amount = earnings.Available,
            Iban = NormalizeIban(request.Iban),
            AccountHolder = request.AccountHolder.Trim(),
            Fin = request.Fin.Trim().ToUpperInvariant()
        };

        _db.PayoutRequests.Add(payout);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Ödəniş tələbi: {UserId}, {Amount} AZN", userId, payout.Amount);
        return ToDto(payout);
    }

    public async Task<IReadOnlyList<AdminPayoutDto>> GetAllAsync(CancellationToken ct)
    {
        var payouts = await _db.PayoutRequests
            .AsNoTracking()
            .Include(p => p.User)
            .OrderBy(p => p.Status)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        return payouts.Select(ToAdminDto).ToList();
    }

    public async Task<AdminPayoutDto> ProcessAsync(Guid id, ProcessPayoutRequest request, CancellationToken ct)
    {
        var payout = await _db.PayoutRequests.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id, ct)
                     ?? throw NotFoundException.For("Ödəniş tələbi", id);

        if (payout.Status != PayoutStatus.Requested)
        {
            throw new ConflictException("Bu tələb artıq icra olunub.");
        }

        if (!request.Approve && string.IsNullOrWhiteSpace(request.Note))
        {
            throw new ValidationFailedException("note", "Rədd səbəbini yazın.");
        }

        payout.Status = request.Approve ? PayoutStatus.Paid : PayoutStatus.Rejected;
        payout.AdminNote = request.Note?.Trim();
        payout.ProcessedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return ToAdminDto(payout);
    }

    private async Task<EarningsDto> BuildEarningsAsync(Guid userId, CancellationToken ct)
    {
        var resourceIds = await _db.Resources.Where(r => r.AuthorId == userId).Select(r => r.Id).ToListAsync(ct);
        var examIds = await _db.Exams.Where(e => e.AuthorId == userId).Select(e => e.Id).ToListAsync(ct);

        var sold = await _db.OrderItems
            .AsNoTracking()
            .Where(oi => oi.Order!.Status == OrderStatus.Paid && oi.AuthorPayoutAmount > 0 &&
                         ((oi.ItemType == CatalogItemType.Resource && resourceIds.Contains(oi.ItemId)) ||
                          (oi.ItemType == CatalogItemType.Exam && examIds.Contains(oi.ItemId))))
            .ToListAsync(ct);

        var items = sold
            .GroupBy(oi => new { oi.ItemType, oi.ItemId })
            .Select(g => new EarningItemDto(
                g.Key.ItemType,
                g.Key.ItemId,
                g.OrderByDescending(i => i.Price).First().Name,
                g.Count(),
                g.Sum(i => i.Price),
                g.Sum(i => i.AuthorPayoutAmount)))
            .OrderByDescending(i => i.Net)
            .ToList();

        var payouts = await _db.PayoutRequests
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        var gross = sold.Sum(i => i.Price);
        var net = sold.Sum(i => i.AuthorPayoutAmount);
        var paidOut = payouts.Where(p => p.Status == PayoutStatus.Paid).Sum(p => p.Amount);
        var pending = payouts.Where(p => p.Status == PayoutStatus.Requested).Sum(p => p.Amount);

        return new EarningsDto(
            gross,
            gross - net,
            net,
            paidOut,
            pending,
            // Qaytarma ödənişdən sonra olarsa balans mənfi düşə bilər - 0-dan aşağı göstərilmir.
            Math.Max(0, net - paidOut - pending),
            MinimumPayout,
            items,
            payouts.Select(ToDto).ToList());
    }

    public static string NormalizeIban(string iban) =>
        new string(iban.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    private static string MaskIban(string iban) =>
        iban.Length <= 8 ? iban : $"{iban[..4]} **** **** {iban[^4..]}";

    private static PayoutDto ToDto(PayoutRequest p) => new(
        p.Id, p.Amount, MaskIban(p.Iban), p.AccountHolder, p.Status, p.AdminNote, p.CreatedAt, p.ProcessedAt);

    private static AdminPayoutDto ToAdminDto(PayoutRequest p) => new(
        p.Id, p.UserId, p.User?.FullName ?? string.Empty, p.User?.Email ?? string.Empty, p.Amount, p.Iban,
        p.AccountHolder, p.Fin, p.Status, p.AdminNote, p.CreatedAt, p.ProcessedAt);
}
