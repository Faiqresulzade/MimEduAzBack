using FluentValidation;
using MimeduAz.Application.Services;
using MimeduAz.Contracts.Payouts;

namespace MimeduAz.Application.Validators;

public sealed class CreatePayoutRequestValidator : AbstractValidator<CreatePayoutRequest>
{
    public CreatePayoutRequestValidator()
    {
        // AZ IBAN: AZ + 2 yoxlama rəqəmi + 4 hərfli bank kodu + 20 simvol = 28.
        RuleFor(x => PayoutService.NormalizeIban(x.Iban))
            .Matches("^AZ[0-9]{2}[A-Z]{4}[A-Z0-9]{20}$")
            .OverridePropertyName(nameof(CreatePayoutRequest.Iban))
            .WithMessage("IBAN düzgün deyil (AZ ilə başlayan 28 simvol).");

        RuleFor(x => x.AccountHolder)
            .NotEmpty().WithMessage("Hesab sahibinin adını yazın.")
            .MaximumLength(150);

        RuleFor(x => x.Fin)
            .Matches("^[A-Za-z0-9]{7}$")
            .WithMessage("FİN kod 7 simvol olmalıdır.");
    }
}

public sealed class ProcessPayoutRequestValidator : AbstractValidator<ProcessPayoutRequest>
{
    public ProcessPayoutRequestValidator()
    {
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
