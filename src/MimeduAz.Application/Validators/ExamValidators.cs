using FluentValidation;
using MimeduAz.Contracts.Exams;

namespace MimeduAz.Application.Validators;

public sealed class CreateExamRequestValidator : AbstractValidator<CreateExamRequest>
{
    public CreateExamRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Sınağın adı boş ola bilməz.").MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().WithMessage("Təsvir boş ola bilməz.").MaximumLength(2000);
        RuleFor(x => x.Subject).NotEmpty().WithMessage("Fənn boş ola bilməz.").MaximumLength(100);

        RuleFor(x => x.Grade)
            .InclusiveBetween(1, 11).When(x => x.Grade.HasValue)
            .WithMessage("Sinif 1-11 aralığında olmalıdır.");

        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(1, 600)
            .WithMessage("Sınağın müddəti 1-600 dəqiqə aralığında olmalıdır.");

        RuleFor(x => x.PassPercent)
            .InclusiveBetween(1, 100)
            .WithMessage("Keçid balı 1-100 aralığında olmalıdır.");

        RuleFor(x => x.Price)
            .GreaterThan(0).When(x => x.IsPaid)
            .WithMessage("Ödənişli sınağın qiyməti 0-dan böyük olmalıdır.");

        RuleFor(x => x.Sections)
            .NotEmpty().WithMessage("Sınaqda ən azı bir fənn bölməsi olmalıdır.");

        RuleForEach(x => x.Sections).SetValidator(new CreateExamSectionRequestValidator());
    }
}

public sealed class UpdateExamRequestValidator : AbstractValidator<UpdateExamRequest>
{
    public UpdateExamRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Sınağın adı boş ola bilməz.").MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().WithMessage("Təsvir boş ola bilməz.").MaximumLength(2000);
        RuleFor(x => x.Subject).NotEmpty().WithMessage("Fənn boş ola bilməz.").MaximumLength(100);

        RuleFor(x => x.Grade)
            .InclusiveBetween(1, 11).When(x => x.Grade.HasValue)
            .WithMessage("Sinif 1-11 aralığında olmalıdır.");

        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(1, 600)
            .WithMessage("Sınağın müddəti 1-600 dəqiqə aralığında olmalıdır.");

        RuleFor(x => x.PassPercent)
            .InclusiveBetween(1, 100)
            .WithMessage("Keçid balı 1-100 aralığında olmalıdır.");

        RuleFor(x => x.Price)
            .GreaterThan(0).When(x => x.IsPaid)
            .WithMessage("Ödənişli sınağın qiyməti 0-dan böyük olmalıdır.");
    }
}

public sealed class SaveExamSectionsRequestValidator : AbstractValidator<SaveExamSectionsRequest>
{
    public SaveExamSectionsRequestValidator()
    {
        RuleFor(x => x.Sections)
            .NotEmpty().WithMessage("Sınaqda ən azı bir fənn bölməsi olmalıdır.");

        RuleForEach(x => x.Sections).SetValidator(new CreateExamSectionRequestValidator());
    }
}

public sealed class CreateExamSectionRequestValidator : AbstractValidator<CreateExamSectionRequest>
{
    public CreateExamSectionRequestValidator()
    {
        RuleFor(x => x.Subject).NotEmpty().WithMessage("Bölmənin fənni boş ola bilməz.").MaximumLength(100);

        RuleFor(x => x.Questions)
            .NotEmpty().WithMessage("Hər bölmədə ən azı bir sual olmalıdır.");

        RuleForEach(x => x.Questions).SetValidator(new CreateExamQuestionRequestValidator());
    }
}

public sealed class CreateExamQuestionRequestValidator : AbstractValidator<CreateExamQuestionRequest>
{
    public CreateExamQuestionRequestValidator()
    {
        // Düsturlu sual tamamilə şəkildən ibarət ola bilər, ona görə mətn tək başına məcburi deyil.
        RuleFor(x => x)
            .Must(q => !string.IsNullOrWhiteSpace(q.QuestionText) || !string.IsNullOrWhiteSpace(q.ImagePath))
            .WithMessage("Sual ya mətn, ya da şəkil ehtiva etməlidir.");

        RuleFor(x => x.QuestionText).MaximumLength(2000);
        RuleFor(x => x.ImagePath).MaximumLength(400);

        RuleFor(x => x.Options)
            .Must(o => o.Count >= 2).WithMessage("Hər sualda ən azı iki variant olmalıdır.")
            .Must(o => o.All(text => !string.IsNullOrWhiteSpace(text)))
                .WithMessage("Variant boş ola bilməz.");

        RuleFor(x => x.CorrectOptionIndex)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Düzgün variantın indeksi mənfi ola bilməz.");

        RuleFor(x => x)
            .Must(q => q.Options.Count == 0 || q.CorrectOptionIndex < q.Options.Count)
            .WithMessage("Düzgün variantın indeksi variant siyahısından kənardadır.");
    }
}

public sealed class SubmitExamRequestValidator : AbstractValidator<SubmitExamRequest>
{
    public SubmitExamRequestValidator()
    {
        RuleForEach(x => x.Answers).ChildRules(a =>
        {
            a.RuleFor(x => x.QuestionId).NotEmpty().WithMessage("Sual id-si boş ola bilməz.");
            a.RuleFor(x => x.SelectedIndex)
                .GreaterThanOrEqualTo(0).When(x => x.SelectedIndex.HasValue)
                .WithMessage("Seçilmiş variantın indeksi mənfi ola bilməz.");
        });
    }
}
