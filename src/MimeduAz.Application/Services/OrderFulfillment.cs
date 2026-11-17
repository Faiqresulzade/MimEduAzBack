using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Options;
using MimeduAz.Domain.Entities;
using MimeduAz.Domain.Enums;

namespace MimeduAz.Application.Services;

/// <summary>
/// Sifarişi "ödənilib" edir və məhsula giriş verir. Həm pulsuz checkout, həm bank təsdiqi,
/// həm də fon status yoxlaması bunu çağırır — ona görə idempotentdir: artıq Paid olan
/// sifarişdə heç nə dəyişmir (giriş, səbət, qəbz iki dəfə verilmir).
/// </summary>
public sealed class OrderFulfillment
{
    private readonly IApplicationDbContext _db;
    private readonly IEmailQueue _emails;
    private readonly SellerOptions _seller;
    private readonly PaymentOptions _payment;
    private readonly ILogger<OrderFulfillment> _logger;

    public OrderFulfillment(
        IApplicationDbContext db,
        IEmailQueue emails,
        IOptions<SellerOptions> seller,
        IOptions<PaymentOptions> payment,
        ILogger<OrderFulfillment> logger)
    {
        _db = db;
        _emails = emails;
        _seller = seller.Value;
        _payment = payment.Value;
        _logger = logger;
    }

    /// <summary>
    /// Sifarişi Paid edir: təlimlərə qeydiyyat açılır, alınan məhsullar səbətdən çıxarılır.
    /// Resurs və sınaqlara giriş Paid sifarişdən oxunur, ayrıca addım lazım deyil.
    /// Müəllif payı da Paid sətirlərdən hesablanır. Dəyişiklik edildisə true.
    /// </summary>
    public async Task<bool> MarkPaidAsync(Order order, CancellationToken ct)
    {
        if (order.Status == OrderStatus.Paid)
        {
            return false;
        }

        order.Status = OrderStatus.Paid;
        order.PaidAt = DateTime.UtcNow;

        var userId = order.UserId;
        if (userId is not null)
        {
            var trainingIds = order.Items
                .Where(i => i.ItemType == CatalogItemType.Training)
                .Select(i => i.ItemId)
                .ToList();

            var enrolled = await _db.Enrollments
                .Where(e => e.UserId == userId && trainingIds.Contains(e.TrainingId))
                .Select(e => e.TrainingId)
                .ToListAsync(ct);

            foreach (var trainingId in trainingIds.Except(enrolled))
            {
                _db.Enrollments.Add(new Enrollment
                {
                    UserId = userId.Value,
                    TrainingId = trainingId,
                    ProgressPercent = 0,
                    Status = EnrollmentStatus.InProgress,
                    EnrolledAt = DateTime.UtcNow
                });
            }

            // Yalnız bu sifarişdə alınan məhsullar səbətdən çıxır - sonradan əlavə edilənlər qalır.
            var boughtKeys = order.Items.Select(i => new { i.ItemType, i.ItemId }).ToList();
            var cartItems = await _db.CartItems
                .Where(ci => ci.Cart!.UserId == userId)
                .ToListAsync(ct);
            _db.CartItems.RemoveRange(cartItems.Where(ci =>
                boughtKeys.Any(k => k.ItemType == ci.ItemType && k.ItemId == ci.ItemId)));
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Sifariş ödənildi. Kod: {Code}, Məbləğ: {Amount}", order.Code, order.TotalAmount);

        if (order.TotalAmount > 0)
        {
            await SendReceiptAsync(order, ct);
        }

        return true;
    }

    /// <summary>Qaytarılan sifarişdə təlim qeydiyyatı bağlanır; resurs/sınaq girişi Paid-dən asılı olduğu üçün özü bağlanır.</summary>
    public async Task RevokeAsync(Order order, CancellationToken ct)
    {
        if (order.UserId is null)
        {
            return;
        }

        var trainingIds = order.Items
            .Where(i => i.ItemType == CatalogItemType.Training)
            .Select(i => i.ItemId)
            .ToList();

        // Eyni təlim başqa ödənilmiş sifarişdə də alınıbsa qeydiyyat qalır.
        var stillPaid = await _db.OrderItems
            .Where(oi =>
                oi.OrderId != order.Id &&
                oi.ItemType == CatalogItemType.Training &&
                trainingIds.Contains(oi.ItemId) &&
                oi.Order!.UserId == order.UserId &&
                oi.Order.Status == OrderStatus.Paid)
            .Select(oi => oi.ItemId)
            .ToListAsync(ct);

        var revoke = trainingIds.Except(stillPaid).ToList();
        var enrollments = await _db.Enrollments
            .Where(e => e.UserId == order.UserId && revoke.Contains(e.TrainingId))
            .ToListAsync(ct);
        _db.Enrollments.RemoveRange(enrollments);
    }

    private async Task SendReceiptAsync(Order order, CancellationToken ct)
    {
        try
        {
            var email = await _db.Users
                .Where(u => u.Id == order.UserId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync(ct);

            if (string.IsNullOrWhiteSpace(email))
            {
                return;
            }

            var az = CultureInfo.InvariantCulture;
            // Bakıda yay vaxtı yoxdur - sabit UTC+4 (ICU/timezone bazasından asılı olmasın).
            var paidAt = (order.PaidAt ?? DateTime.UtcNow).AddHours(4);
            var rows = string.Join(string.Empty, order.Items.Select(i =>
                $"<tr><td>{WebUtility.HtmlEncode(i.Name)}</td><td align=\"right\">{i.Price.ToString("0.00", az)} AZN</td></tr>"));

            var body = $"""
                <div style="font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1F2937">
                  <h2 style="color:#1B2A5B;margin:0 0 12px">Ödəniş qəbzi — sifariş {WebUtility.HtmlEncode(order.Code)}</h2>
                  <p>Tarix: {paidAt:dd.MM.yyyy HH:mm} (Bakı vaxtı)</p>
                  <table cellpadding="6" style="border-collapse:collapse;min-width:320px">
                    {rows}
                    <tr><td><b>Cəmi</b></td><td align="right"><b>{order.TotalAmount.ToString("0.00", az)} AZN</b></td></tr>
                  </table>
                  <p>
                    Kart: {WebUtility.HtmlEncode(order.CardMask ?? "—")}<br>
                    RRN: {WebUtility.HtmlEncode(order.Rrn ?? "—")} · Təsdiq kodu: {WebUtility.HtmlEncode(order.ApprovalCode ?? "—")}
                  </p>
                  <p>Alınan məhsullar hesabınızda açıqdır: <a href="{_payment.SiteUrl.TrimEnd('/')}/sifarisler">Sifarişlərim</a></p>
                  <p style="color:#6B7280;font-size:12px">
                    Satıcı: {WebUtility.HtmlEncode(_seller.Name)} · VÖEN: {WebUtility.HtmlEncode(_seller.Voen)}<br>
                    {WebUtility.HtmlEncode(_seller.Address)} · {WebUtility.HtmlEncode(_seller.Phone)} · {WebUtility.HtmlEncode(_seller.Email)}
                  </p>
                </div>
                """;

            if (!_emails.TryEnqueue(new EmailMessage(new[] { email }, $"MIMEDU.AZ — ödəniş qəbzi {order.Code}", body)))
            {
                _logger.LogWarning("Qəbz məktubu növbəyə düşmədi. Sifariş: {Code}", order.Code);
            }
        }
        catch (Exception ex)
        {
            // Qəbz göndərilməsə də ödəniş artıq qeydə alınıb.
            _logger.LogError(ex, "Qəbz məktubu hazırlanmadı. Sifariş: {Code}", order.Code);
        }
    }
}
