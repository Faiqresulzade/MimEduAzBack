namespace MimeduAz.Domain.Constants;

public static class AppRoles
{
    /// <summary>Təlim alan/keçən istifadəçi. Material yükləyə bilmir.</summary>
    public const string Student = "Student";

    /// <summary>Şagirdin bütün imkanları + Resurs Bankına material yükləmək.</summary>
    public const string Teacher = "Teacher";

    public const string Admin = "Admin";

    public static readonly string[] All = { Student, Teacher, Admin };

    /// <summary>Resurs yükləyə/sata bilən rollar.</summary>
    public const string AuthorRoles = Teacher + "," + Admin;
}

public static class AppDefaults
{
    /// <summary>Bu e-poçt ilə qeydiyyatdan keçən istifadəçi avtomatik Admin rolu alır.</summary>
    public const string AdminEmail = "admin@mimedu.az";
}
