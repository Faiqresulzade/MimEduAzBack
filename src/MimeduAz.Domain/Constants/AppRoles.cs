namespace MimeduAz.Domain.Constants;

public static class AppRoles
{
    public const string Teacher = "Teacher";
    public const string Admin = "Admin";

    public static readonly string[] All = { Teacher, Admin };
}

public static class AppDefaults
{
    /// <summary>Bu e-poçt ilə qeydiyyatdan keçən istifadəçi avtomatik Admin rolu alır.</summary>
    public const string AdminEmail = "admin@mimedu.az";
}
