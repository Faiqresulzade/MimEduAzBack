namespace MimeduAz.Application.Common.Interfaces;

/// <summary>Cari HTTP sorğusunu göndərən istifadəçi haqqında məlumat.</summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }

    /// <summary>Autentifikasiya tələb olunan yerlərdə istifadəçi Id-sini qaytarır, yoxdursa xəta atır.</summary>
    Guid RequireUserId();
}
