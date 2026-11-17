using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Mappings;
using MimeduAz.Application.Common.Options;
using MimeduAz.Application.Common.Utilities;
using MimeduAz.Contracts.Orders;
using MimeduAz.Domain.Entities;
using MimeduAz.Domain.Enums;

namespace MimeduAz.Application.Services;

public sealed class OrderService : IOrderService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly CommissionOptions _commission;
    private readonly PaymentOptions _payment;
    private readonly IPaymentGateway _gateway;
    private readonly OrderFulfillment _fulfillment;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IOptions<CommissionOptions> commission,
        IOptions<PaymentOptions> payment,
        IPaymentGateway gateway,
        OrderFulfillment fulfillment,
        ILogger<OrderService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _commission = commission.Value;
        _payment = payment.Value;
        _gateway = gateway;
        _fulfillment = fulfillment;
        _logger = logger;
    }

    public async Task<CheckoutResponse> CheckoutAsync(CheckoutRequest request, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new NotFoundException("İstifadəçi tapılmadı.");

        var cart = await _db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);

        if (cart is null || cart.Items.Count == 0)
        {
            throw new BadRequestException("Səbət boşdur.");
        }

        var items = cart.Items.OrderBy(i => i.AddedAt).ToList();

        var resourceIds = items.Where(i => i.ItemType == CatalogItemType.Resource).Select(i => i.ItemId).ToList();
        var trainingIds = items.Where(i => i.ItemType == CatalogItemType.Training).Select(i => i.ItemId).ToList();
        var examIds = items.Where(i => i.ItemType == CatalogItemType.Exam).Select(i => i.ItemId).ToList();

        var resources = await _db.Resources
            .Where(r => resourceIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, ct);

        var trainings = await _db.Trainings
            .Where(t => trainingIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, ct);

        var exams = await _db.Exams
            .Where(e => examIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, ct);

        var order = new Order
        {
            Code = await GenerateOrderCodeAsync(ct),
            UserId = userId,
            CustomerName = string.IsNullOrWhiteSpace(request.CustomerName)
                ? user.FullName
                : request.CustomerName.Trim(),
            // Giriş yalnız Paid-də açılır: pulsuz səbət dərhal, ödənişli isə bank təsdiqindən sonra.
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var item in items)
        {
            if (item.ItemType == CatalogItemType.Resource)
            {
                if (!resources.TryGetValue(item.ItemId, out var resource))
                {
                    throw new BadRequestException("Səbətdəki resurslardan biri artıq mövcud deyil.");
                }

                // Ödənişli resursdan platforma komissiya tutur, qalanı müəllifə qalır.
                var commission = Math.Round(item.Price * _commission.ResourcePercent, 2, MidpointRounding.AwayFromZero);

                order.Items.Add(new OrderItem
                {
                    OrderId = order.Id,
                    ItemType = CatalogItemType.Resource,
                    ItemId = resource.Id,
                    Name = resource.Name,
                    Price = item.Price,
                    CommissionAmount = commission,
                    AuthorPayoutAmount = item.Price - commission
                });
            }
            else if (item.ItemType == CatalogItemType.Exam)
            {
                if (!exams.TryGetValue(item.ItemId, out var exam))
                {
                    throw new BadRequestException("Səbətdəki sınaqlardan biri artıq mövcud deyil.");
                }

                // Sınağı da müəllim yaradıb satır - resurslardakı kimi komissiya tutulur.
                var examCommission = Math.Round(
                    item.Price * _commission.ExamPercent, 2, MidpointRounding.AwayFromZero);

                order.Items.Add(new OrderItem
                {
                    OrderId = order.Id,
                    ItemType = CatalogItemType.Exam,
                    ItemId = exam.Id,
                    Name = exam.Name,
                    Price = item.Price,
                    CommissionAmount = examCommission,
                    AuthorPayoutAmount = item.Price - examCommission
                });
            }
            else
            {
                if (!trainings.TryGetValue(item.ItemId, out var training))
                {
                    throw new BadRequestException("Səbətdəki təlimlərdən biri artıq mövcud deyil.");
                }

                if (training.SeatLimit is not null)
                {
                    var seatsTaken = await _db.Enrollments.CountAsync(e => e.TrainingId == training.Id, ct);
                    if (seatsTaken >= training.SeatLimit.Value)
                    {
                        throw new ConflictException($"«{training.Name}» təlimində boş yer qalmayıb.");
                    }
                }

                order.Items.Add(new OrderItem
                {
                    OrderId = order.Id,
                    ItemType = CatalogItemType.Training,
                    ItemId = training.Id,
                    Name = training.Name,
                    Price = item.Price,
                    // Təlim platformanın öz məhsuludur - müəllif payı yoxdur.
                    CommissionAmount = 0m,
                    AuthorPayoutAmount = 0m
                });
            }
        }

        order.TotalAmount = order.Items.Sum(i => i.Price);
        order.CommissionAmount = order.Items.Sum(i => i.CommissionAmount);

        // Pulsuz səbət (məs. pulsuz təlimlər): bank lazım deyil, dərhal tamamlanır.
        if (order.TotalAmount <= 0)
        {
            _db.Orders.Add(order);
            await _fulfillment.MarkPaidAsync(order, ct);
            return new CheckoutResponse(order.ToDto(), null);
        }

        if (!request.AcceptTerms)
        {
            throw new ValidationFailedException(
                "acceptTerms", "Ödəniş üçün istifadə şərtləri, geri qaytarma və xidmətin təqdimi qaydaları ilə razılaşmalısınız.");
        }

        if (!_payment.Enabled || !_gateway.IsConfigured)
        {
            throw new ConflictException(
                "Kartla ödəniş tezliklə aktiv olacaq. Hazırda yalnız pulsuz məhsulları əldə etmək mümkündür.");
        }

        order.TermsAcceptedAt = DateTime.UtcNow;
        order.TermsVersion = _payment.TermsVersion;
        order.PaymentNumber = await GeneratePaymentNumberAsync(ct);

        // Səbət ödəniş təsdiqlənənə qədər qalır - bankdan geri qayıdan istifadəçi yenidən cəhd edə bilər.
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        var redirect = _gateway.CreateSale(order, $"MIMEDU.AZ sifariş {order.Code}", user.Email, request.ClientInfo);

        _db.PaymentLogs.Add(new PaymentLog
        {
            OrderId = order.Id,
            Kind = "Request",
            // İmza, nonce və brauzer məlumatı olmadan - nəyin göndərildiyini göstərmək kifayətdir.
            Payload = System.Text.Json.JsonSerializer.Serialize(
                redirect.Fields.Where(f => f.Key is not ("P_SIGN" or "NONCE" or "M_INFO"))
                    .ToDictionary(f => f.Key, f => f.Value))
        });
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Ödəniş başladıldı. Sifariş: {Code}, ORDER: {PaymentNumber}, Məbləğ: {Amount}",
            order.Code, order.PaymentNumber, order.TotalAmount);

        return new CheckoutResponse(order.ToDto(), new PaymentRedirectDto(redirect.Url, redirect.Fields));
    }

    public async Task<OrderDto> GetMineByCodeAsync(string code, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Code == code && o.UserId == userId, ct)
            ?? throw new NotFoundException("Sifariş tapılmadı.");

        return order.ToDto();
    }

    public async Task<IReadOnlyList<OrderDto>> GetMineAsync(CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var orders = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

        return orders.Select(o => o.ToDto()).ToList();
    }

    /// <summary>
    /// AzeriCard ORDER: yalnız rəqəm, 6–20 simvol, terminal üzrə gün ərzində unikal
    /// (son 6 rəqəm bankda audit nömrəsidir). yyMMddHHmmss + 4 təsadüfi rəqəm = 16 rəqəm.
    /// </summary>
    private async Task<string> GeneratePaymentNumberAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var number = DateTime.UtcNow.ToString("yyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture)
                         + System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 10000).ToString("D4");
            if (!await _db.Orders.AnyAsync(o => o.PaymentNumber == number, ct))
            {
                return number;
            }
        }

        throw new ConflictException("Sifariş nömrəsi yaradılmadı. Yenidən cəhd edin.");
    }

    private Task<string> GenerateOrderCodeAsync(CancellationToken ct) =>
        CodeFactory.UniqueAsync(
            CodeFactory.NewOrderCode,
            (code, token) => _db.Orders.AnyAsync(o => o.Code == code, token),
            CodeFactory.NewOrderCodeLong,
            ct);
}
