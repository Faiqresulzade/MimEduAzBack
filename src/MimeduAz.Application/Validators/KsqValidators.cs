using FluentValidation;
using MimeduAz.Application.Common.Utilities;
using MimeduAz.Contracts.Ksq;

namespace MimeduAz.Application.Validators;

public sealed class SaveKsqTestRequestValidator : AbstractValidator<SaveKsqTestRequest>
{
    public SaveKsqTestRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Testin adını yazın.").MaximumLength(200);

        RuleFor(x => x.ClosedPoints.Count)
            .InclusiveBetween(1, KsqScoring.MaxClosed)
            .OverridePropertyName(nameof(SaveKsqTestRequest.ClosedPoints))
            .WithMessage($"Qapalı sual sayı 1-{KsqScoring.MaxClosed} aralığında olmalıdır.");

        RuleFor(x => x.OpenPoints.Count)
            .InclusiveBetween(0, KsqScoring.MaxOpen)
            .OverridePropertyName(nameof(SaveKsqTestRequest.OpenPoints))
            .WithMessage($"Açıq sual sayı 0-{KsqScoring.MaxOpen} aralığında olmalıdır.");

        RuleForEach(x => x.ClosedPoints)
            .InclusiveBetween(0, 100).WithMessage("Sualın balı 0-100 aralığında olmalıdır.");

        RuleForEach(x => x.OpenPoints)
            .InclusiveBetween(0, 100).WithMessage("Sualın balı 0-100 aralığında olmalıdır.");

        // Preset və Sərbəst eyni yoxlamadan keçir: açarın hər qapalı sual üçün bir yeri olmalıdır
        // (cavab seçilməyibsə null).
        RuleFor(x => x.KeyA)
            .Must((request, key) => key.Count == request.ClosedPoints.Count)
            .WithMessage("A variantının açarı qapalı sual sayı qədər olmalıdır.");

        RuleFor(x => x.KeyB)
            .Must((request, key) => key.Count == request.ClosedPoints.Count)
            .WithMessage("B variantının açarı qapalı sual sayı qədər olmalıdır.");

        RuleForEach(x => x.KeyA).Must(BeLetterOrNull).WithMessage("Cavab A, B, C, D və ya E olmalıdır.");
        RuleForEach(x => x.KeyB).Must(BeLetterOrNull).WithMessage("Cavab A, B, C, D və ya E olmalıdır.");
    }

    internal static bool BeLetterOrNull(string? value) =>
        value is null || KsqScoring.NormalizeLetter(value) is not null;
}

public sealed class SaveKsqResultRequestValidator : AbstractValidator<SaveKsqResultRequest>
{
    public SaveKsqResultRequestValidator()
    {
        RuleFor(x => x.TestId).NotEmpty().WithMessage("Test seçilməyib.");
        RuleFor(x => x.Variant)
            .Must(v => v is not null && v.Trim().ToUpperInvariant() is "A" or "B")
            .WithMessage("Variant A və ya B olmalıdır.");

        RuleFor(x => x.Sinif).MaximumLength(20).WithMessage("Sinif 20 simvoldan uzun ola bilməz.");
        RuleFor(x => x.Ad).MaximumLength(200).WithMessage("Ad 200 simvoldan uzun ola bilməz.");
        RuleFor(x => x.Kod).MaximumLength(50).WithMessage("Kod 50 simvoldan uzun ola bilməz.");

        RuleFor(x => x.Cavablar.Count)
            .LessThanOrEqualTo(KsqScoring.MaxClosed)
            .OverridePropertyName(nameof(SaveKsqResultRequest.Cavablar))
            .WithMessage($"Ən çox {KsqScoring.MaxClosed} cavab ola bilər.");

        RuleFor(x => x.Aciq.Count)
            .LessThanOrEqualTo(KsqScoring.MaxOpen)
            .OverridePropertyName(nameof(SaveKsqResultRequest.Aciq))
            .WithMessage($"Ən çox {KsqScoring.MaxOpen} açıq sual ola bilər.");

        RuleForEach(x => x.Cavablar)
            .Must(SaveKsqTestRequestValidator.BeLetterOrNull)
            .WithMessage("Cavab A, B, C, D və ya E olmalıdır.");
    }
}

public sealed class SaveKsqResultsBulkRequestValidator : AbstractValidator<SaveKsqResultsBulkRequest>
{
    public SaveKsqResultsBulkRequestValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Saxlanacaq nəticə yoxdur.")
            .Must(items => items.Count <= 200).WithMessage("Bir dəfəyə ən çox 200 nəticə saxlamaq olar.");

        RuleForEach(x => x.Items).SetValidator(new SaveKsqResultRequestValidator());
    }
}
