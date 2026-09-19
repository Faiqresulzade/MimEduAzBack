namespace MimeduAz.Domain.Enums;

/// <summary>Sınağın moderasiya vəziyyəti. Yalnız <see cref="Approved"/> olan sınaq satışa çıxır.</summary>
public enum ExamStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

/// <summary>İştirakçının sınaq cəhdinin vəziyyəti.</summary>
public enum ExamAttemptStatus
{
    /// <summary>Başlanıb, vaxtı hələ bitməyib, cavablar göndərilməyib.</summary>
    InProgress = 0,

    /// <summary>Vaxtında təhvil verilib və qiymətləndirilib.</summary>
    Submitted = 1,

    /// <summary>Vaxt bitdiyi üçün qiymətləndirilməyib (bal 0, sertifikat verilmir).</summary>
    Expired = 2
}
