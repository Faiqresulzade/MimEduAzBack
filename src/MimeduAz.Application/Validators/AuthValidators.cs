using FluentValidation;
using MimeduAz.Contracts.Auth;

namespace MimeduAz.Application.Validators;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Ad və soyad boş ola bilməz.")
            .MaximumLength(150).WithMessage("Ad və soyad 150 simvoldan uzun ola bilməz.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-poçt boş ola bilməz.")
            .EmailAddress().WithMessage("E-poçt ünvanı düzgün deyil.")
            .MaximumLength(256);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Şifrə boş ola bilməz.")
            .MinimumLength(8).WithMessage("Şifrə ən azı 8 simvol olmalıdır.")
            .Matches("[A-Za-z]").WithMessage("Şifrədə ən azı bir hərf olmalıdır.")
            .Matches("[0-9]").WithMessage("Şifrədə ən azı bir rəqəm olmalıdır.");

        RuleFor(x => x.Subject)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.Subject));
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("E-poçt boş ola bilməz.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Şifrə boş ola bilməz.");
    }
}

public sealed class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator() =>
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("Refresh token boş ola bilməz.");
}

public sealed class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator() =>
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("Refresh token boş ola bilməz.");
}
