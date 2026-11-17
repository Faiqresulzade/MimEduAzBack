using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Mappings;
using MimeduAz.Application.Common.Options;
using MimeduAz.Contracts.Orders;
using MimeduAz.Domain.Entities;
using MimeduAz.Domain.Enums;

namespace MimeduAz.Application.Services;

/// <summary>Callback emalının nəticəsi — brauzeri hansı səhifəyə qaytarmaq lazımdır.</summary>
public sealed record PaymentCallbackOutcome(string? OrderCode, OrderStatus? Status);

public interface IPaymentService
{
    PaymentConfigDto GetConfig();

    /// <summary>AzeriCard-ın BACKREF-ə göndərdiyi nəticəni emal edir. İdempotentdir.</summary>
    Task<PaymentCallbackOutcome> HandleCallbackAsync(IReadOnlyDictionary<string, string> fields, CancellationToken ct);

    /// <summary>Fon işi: tamamlanmamış və statusu bilinməyən sifarişləri bankdan yoxlayır.</summary>
    Task ProcessPendingAsync(CancellationToken ct);

    /// <summary>Admin: ödənişi geri qaytarır (TRTYPE=22) və girişi bağlayır.</summary>
    Task<OrderDto> RefundAsync(Guid orderId, CancellationToken ct);
}

/// <summary>
/// Sifariş yalnız bankın imzası yoxlanmış təsdiqi ilə Paid olur. Callback imzası ORDER və ACTION
/// sahələrini əhatə etmədiyi üçün (AzeriCard sənədi, bölmə 2) əlavə qoruma var: məbləğ və terminal
/// bazadakı ilə tutuşdurulur, bir bank əməliyyatı (INT_REF) yalnız bir sifarişi ödəyə bilər və
/// giriş yalnız TRTYPE=21 tamamlaması uğurlu olandan sonra açılır.
/// </summary>
public sealed class PaymentService : IPaymentService
{
    private readonly IApplicationDbContext _db;
    private readonly IPaymentGateway _gateway;
    private readonly OrderFulfillment _fulfillment;
    private readonly PaymentOptions _options;
    private readonly AzeriCardOptions _azeriCard;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IApplicationDbContext db,
        IPaymentGateway gateway,
        OrderFulfillment fulfillment,
        IOptions<PaymentOptions> options,
        IOptions<AzeriCardOptions> azeriCard,
        ILogger<PaymentService> logger)
    {
        _db = db;
        _gateway = gateway;
        _fulfillment = fulfillment;
        _options = options.Value;
        _azeriCard = azeriCard.Value;
        _logger = logger;
    }

    public PaymentConfigDto GetConfig() =>
        new(_options.Enabled && _gateway.IsConfigured, _azeriCard.Currency, _options.TermsVersion);

    public async Task<PaymentCallbackOutcome> HandleCallbackAsync(
        IReadOnlyDictionary<string, string> fields, CancellationToken ct)
    {
        var result = _gateway.ParseCallback(fields);

        var order = string.IsNullOrWhiteSpace(result.OrderNumber)
            ? null
            : await _db.Orders.Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.PaymentNumber == result.OrderNumber, ct);

        await LogAsync(order?.Id, "Callback", result, ct);

        if (order is null)
        {
            _logger.LogWarning("AzeriCard callback: sifariş tapılmadı (ORDER {Order}).", result.OrderNumber);
            return new PaymentCallbackOutcome(null, null);
        }

        await ApplyAuthorizationAsync(order, result, ct);
        return new PaymentCallbackOutcome(order.Code, order.Status);
    }

    /// <summary>Bankın avtorizasiya cavabını sifarişə tətbiq edir (callback və ya status sorğusu).</summary>
    private async Task ApplyAuthorizationAsync(Order order, PaymentResult result, CancellationToken ct)
    {
        // Artıq yekun vəziyyətdədirsə təkrar callback heç nə dəyişmir.
        if (order.Status is OrderStatus.Paid or OrderStatus.Refunded)
        {
            return;
        }

        order.PaymentResult = Describe(result);

        if (!result.IsApproved)
        {
            // İmzasız "uğursuz" cavaba da inanmaq təhlükəsizdir - giriş açılmır, istifadəçi yenidən cəhd edə bilər.
            if (result.Action is "2" or "3")
            {
                order.Status = OrderStatus.Failed;
            }

            await _db.SaveChangesAsync(ct);
            return;
        }

        var problem = Validate(order, result);
        if (problem is not null)
        {
            _logger.LogWarning("AzeriCard təsdiqi rədd edildi. Sifariş {Code}: {Problem}", order.Code, problem);
            order.PaymentResult = $"Rədd edildi: {problem}";
            await _db.SaveChangesAsync(ct);
            return;
        }

        if (await _db.Orders.AnyAsync(o => o.Id != order.Id && o.IntRef == result.IntRef, ct))
        {
            _logger.LogWarning("AzeriCard: INT_REF {IntRef} artıq başqa sifarişdə istifadə olunub.", result.IntRef);
            order.PaymentResult = "Rədd edildi: əməliyyat artıq başqa sifarişə aiddir";
            await _db.SaveChangesAsync(ct);
            return;
        }

        order.Rrn = result.Rrn;
        order.IntRef = result.IntRef;
        order.ApprovalCode = result.Approval;
        order.CardMask = result.CardMask ?? order.CardMask;
        await _db.SaveChangesAsync(ct);

        await CompleteAsync(order, ct);
    }

    /// <summary>TRTYPE=21: uğurlu avtorizasiyanı tamamlayır, uğurludursa giriş açılır.</summary>
    private async Task CompleteAsync(Order order, CancellationToken ct)
    {
        PaymentResult completion;
        try
        {
            completion = await _gateway.CompleteAsync(order, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Sifariş Pending + INT_REF ilə qalır - fon işi tamamlamanı təkrarlayacaq.
            _logger.LogWarning("AzeriCard tamamlaması alınmadı ({Code}): {Error}", order.Code, ex.Message);
            order.PaymentResult = "Tamamlama gözləyir";
            await _db.SaveChangesAsync(ct);
            return;
        }

        await LogAsync(order.Id, "Completion", completion, ct);

        // ACTION=1 - eyni tamamlama artıq edilib (təkrar sorğu).
        if (completion.Action is "0" or "1")
        {
            await _fulfillment.MarkPaidAsync(order, ct);
            return;
        }

        order.PaymentResult = $"Tamamlama: {Describe(completion)}";
        await _db.SaveChangesAsync(ct);
    }

    public async Task ProcessPendingAsync(CancellationToken ct)
    {
        if (!_gateway.IsConfigured)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var minAge = now.AddMinutes(-_options.StatusCheckMinAgeMinutes);
        var expiry = now.AddHours(-_options.PendingExpiryHours);

        var pending = await _db.Orders
            .Include(o => o.Items)
            .Where(o => o.Status == OrderStatus.Pending && o.PaymentNumber != null && o.CreatedAt < minAge)
            .OrderBy(o => o.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        foreach (var order in pending)
        {
            try
            {
                if (order.IntRef is not null)
                {
                    // Avtorizasiya var, tamamlama alınmamışdı.
                    await CompleteAsync(order, ct);
                }
                else if (order.CreatedAt < expiry)
                {
                    // Bank status sorğusunu yalnız 24 saat qəbul edir.
                    order.Status = OrderStatus.Cancelled;
                    order.PaymentResult ??= "Ödəniş edilmədi (vaxt bitdi)";
                    await _db.SaveChangesAsync(ct);
                }
                else
                {
                    var status = await _gateway.QueryStatusAsync(order, ct);
                    await LogAsync(order.Id, "StatusCheck", status, ct);

                    // Status cavabının imzası callback-dəki kimi yoxlanılır; imzasız "təsdiq"ə inanılmır.
                    if (status.IsApproved && status.SignatureValid)
                    {
                        await ApplyAuthorizationAsync(order, status, ct);
                    }
                    else if (status.Action is "2" or "3")
                    {
                        order.Status = OrderStatus.Failed;
                        order.PaymentResult = Describe(status);
                        await _db.SaveChangesAsync(ct);
                    }
                }
            }
            catch (Exception ex)
            {
                // Bir sifarişin xətası digərlərini dayandırmasın.
                _logger.LogError(ex, "Pending sifariş yoxlanmadı: {Code}", order.Code);
            }
        }
    }

    public async Task<OrderDto> RefundAsync(Guid orderId, CancellationToken ct)
    {
        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId, ct)
                    ?? throw NotFoundException.For("Sifariş", orderId);

        if (order.Status != OrderStatus.Paid)
        {
            throw new ConflictException("Yalnız ödənilmiş sifariş qaytarıla bilər.");
        }

        if (order.TotalAmount > 0)
        {
            if (!_gateway.IsConfigured || order.IntRef is null)
            {
                throw new ConflictException("Bu sifariş kartla ödənilməyib və ya ödəniş sistemi qoşulmayıb.");
            }

            var refund = await _gateway.RefundAsync(order, ct);
            await LogAsync(order.Id, "Refund", refund, ct);

            if (refund.Action is not ("0" or "1"))
            {
                throw ExternalServiceException.BadGateway($"Bank qaytarmanı qəbul etmədi: {Describe(refund)}");
            }
        }

        // Resurs/sınaq girişi və müəllif payı Paid-dən hesablandığı üçün status dəyişən kimi bağlanır.
        order.Status = OrderStatus.Refunded;
        order.PaymentResult = "Qaytarıldı";
        await _fulfillment.RevokeAsync(order, ct);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Sifariş qaytarıldı: {Code}", order.Code);
        return order.ToDto();
    }

    /// <summary>İmzası, məbləği, valyutası və terminalı bazadakı sifarişlə uyğun gəlirmi.</summary>
    private string? Validate(Order order, PaymentResult result)
    {
        if (!result.SignatureValid)
        {
            return "imza yanlışdır";
        }

        if (string.IsNullOrWhiteSpace(result.IntRef) || string.IsNullOrWhiteSpace(result.Rrn))
        {
            return "RRN/INT_REF yoxdur";
        }

        if (!decimal.TryParse(result.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            || amount != order.TotalAmount)
        {
            return $"məbləğ uyğun deyil ({result.Amount} ≠ {order.TotalAmount:0.00})";
        }

        if (!string.Equals(result.Terminal, _azeriCard.Terminal, StringComparison.Ordinal))
        {
            return "terminal uyğun deyil";
        }

        // Cavabda valyuta rəqəmlə (944) də gələ bilər.
        if (result.Currency is not null && result.Currency is not ("AZN" or "944"))
        {
            return $"valyuta uyğun deyil ({result.Currency})";
        }

        return null;
    }

    private static string Describe(PaymentResult result) =>
        $"ACTION={result.Action ?? "-"}, RC={result.ResponseCode ?? "-"}";

    private async Task LogAsync(Guid? orderId, string kind, PaymentResult result, CancellationToken ct)
    {
        // Bank kart nömrəsini artıq maskalı qaytarır; imza və nonce saxlanmır.
        var payload = result.RawFields
            .Where(f => f.Key is not ("P_SIGN" or "NONCE"))
            .ToDictionary(f => f.Key, f => f.Value);

        _db.PaymentLogs.Add(new PaymentLog
        {
            OrderId = orderId,
            Kind = kind,
            Payload = JsonSerializer.Serialize(payload),
            SignatureValid = result.SignatureValid
        });
        await _db.SaveChangesAsync(ct);
    }
}
