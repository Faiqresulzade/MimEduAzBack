namespace MimeduAz.Domain.Entities;

/// <summary>
/// Bir HTTP sorğusunun audit qeydi: metod, yol, status, müddət, istifadəçi və
/// (maskalanmış) request/response gövdəsi.
/// Şifrə, token və digər həssas sahələr yazılmazdan əvvəl "***" ilə əvəz olunur.
/// </summary>
public class RequestLog
{
    /// <summary>Yüksək həcmli əlavə-only cədvəl olduğu üçün Guid deyil, auto-increment.</summary>
    public long Id { get; set; }

    /// <summary>Serilog qeydləri ilə uyğunlaşdırmaq üçün sorğu identifikatoru.</summary>
    public string TraceId { get; set; } = string.Empty;

    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? QueryString { get; set; }

    public int StatusCode { get; set; }
    public int DurationMs { get; set; }

    /// <summary>Autentifikasiya olunmuş istifadəçi (varsa).</summary>
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public string? RequestContentType { get; set; }

    /// <summary>Maskalanmış və uzunluğu məhdudlaşdırılmış request gövdəsi.</summary>
    public string? RequestBody { get; set; }

    /// <summary>Maskalanmış və uzunluğu məhdudlaşdırılmış response gövdəsi.</summary>
    public string? ResponseBody { get; set; }

    /// <summary>Sorğu exception ilə bitibsə - exception tipinin adı.</summary>
    public string? ExceptionType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
