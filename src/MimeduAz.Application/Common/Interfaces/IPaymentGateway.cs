using MimeduAz.Domain.Entities;

namespace MimeduAz.Application.Common.Interfaces;

/// <summary>Brauzerin avtomatik POST formu ilə bank səhifəsinə göndəriləcəyi sorğu.</summary>
public sealed record PaymentRedirect(string Url, IReadOnlyDictionary<string, string> Fields);

/// <summary>
/// Bankın cavabı (callback və ya server-server sorğusunun nəticəsi).
/// <see cref="SignatureValid"/> false olarsa heç bir sahəyə güvənilmir.
/// </summary>
public sealed record PaymentResult(
    bool SignatureValid,
    string? OrderNumber,
    string? Amount,
    string? Currency,
    string? Terminal,
    string? Action,
    string? ResponseCode,
    string? Rrn,
    string? IntRef,
    string? Approval,
    string? CardMask,
    IReadOnlyDictionary<string, string> RawFields)
{
    /// <summary>ACTION=0 və RC=00 — bank əməliyyatı təsdiqlədi.</summary>
    public bool IsApproved => Action == "0" && ResponseCode == "00";
}

/// <summary>
/// Kart ödənişi provayderi (AzeriCard). Kart məlumatı heç vaxt bizdən keçmir —
/// istifadəçi bankın səhifəsinə yönləndirilir.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Terminal və açarlar qurulubmu.</summary>
    bool IsConfigured { get; }

    /// <summary>Satış (TRTYPE=1) üçün imzalanmış form sahələri.</summary>
    PaymentRedirect CreateSale(Order order, string description, string? email, string? clientInfo);

    /// <summary>Bankın BACKREF-ə göndərdiyi sahələri oxuyur və imzanı yoxlayır.</summary>
    PaymentResult ParseCallback(IReadOnlyDictionary<string, string> fields);

    /// <summary>Uğurlu avtorizasiyanı tamamlayır (TRTYPE=21).</summary>
    Task<PaymentResult> CompleteAsync(Order order, CancellationToken ct);

    /// <summary>Statusu bankdan soruşur (TRTYPE=90, ilk 24 saat ərzində).</summary>
    Task<PaymentResult> QueryStatusAsync(Order order, CancellationToken ct);

    /// <summary>Ödənişi geri qaytarır (TRTYPE=22, online reversal).</summary>
    Task<PaymentResult> RefundAsync(Order order, CancellationToken ct);
}
