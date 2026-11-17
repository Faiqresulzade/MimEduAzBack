namespace MimeduAz.Domain.Enums;

public enum PayoutStatus
{
    /// <summary>Müəllim tələb göndərib, admin hələ köçürməyib.</summary>
    Requested = 0,
    /// <summary>Admin bank köçürməsini edib.</summary>
    Paid = 1,
    /// <summary>Admin rədd edib (məs. IBAN səhvdir) — məbləğ balansa qayıdır.</summary>
    Rejected = 2
}
