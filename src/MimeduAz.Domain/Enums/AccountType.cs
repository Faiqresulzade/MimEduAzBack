namespace MimeduAz.Domain.Enums;

/// <summary>Qeydiyyat zamanı seçilən hesab növü.</summary>
public enum AccountType
{
    /// <summary>Yalnız təlim alır və keçir; material satmır.</summary>
    Student = 0,

    /// <summary>Şagirdin bacardığı hər şey + Resurs Bankına material yükləyib sata bilir.</summary>
    Teacher = 1
}
