namespace MimeduAz.Domain.Entities;

/// <summary>
/// AzeriCard ilə hər mübadilənin xam qeydi (sorğu, callback, status yoxlaması, qaytarma).
/// Mübahisə zamanı sübut üçündür. Kart nömrəsi bank tərəfindən artıq maskalanmış gəlir.
/// </summary>
public class PaymentLog
{
    public long Id { get; set; }

    public Guid? OrderId { get; set; }

    /// <summary>Request, Callback, StatusCheck, Refund.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Göndərilən/alınan sahələr (JSON).</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>Callback/cavab imzası yoxlandısa nəticə; sorğularda null.</summary>
    public bool? SignatureValid { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
