using FluentValidation;
using MimeduAz.Contracts.Blog;
using MimeduAz.Contracts.Carts;
using MimeduAz.Contracts.Certificates;
using MimeduAz.Contracts.Quizzes;
using MimeduAz.Contracts.Resources;
using MimeduAz.Contracts.Trainings;
using MimeduAz.Domain.Enums;

namespace MimeduAz.Application.Validators;

public sealed class CreateResourceRequestValidator : AbstractValidator<CreateResourceRequest>
{
    public CreateResourceRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Resursun adı boş ola bilməz.")
            .MaximumLength(200).WithMessage("Resursun adı 200 simvoldan uzun ola bilməz.");

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Fənn seçilməlidir.")
            .MaximumLength(100);

        RuleFor(x => x.Grade)
            .InclusiveBetween(1, 11).WithMessage("Sinif 1 ilə 11 arasında olmalıdır.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Resurs növü düzgün deyil.");

        RuleFor(x => x.Price)
            .GreaterThan(0).When(x => x.IsPaid)
            .WithMessage("Ödənişli resursun qiyməti 0-dan böyük olmalıdır.");

        RuleFor(x => x.Price)
            .LessThanOrEqualTo(1000).WithMessage("Qiymət 1000 AZN-dən çox ola bilməz.");
    }
}

public sealed class CreateTrainingRequestValidator : AbstractValidator<CreateTrainingRequest>
{
    public CreateTrainingRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Təlimin adı boş ola bilməz.").MaximumLength(200);
        RuleFor(x => x.Format).IsInEnum().WithMessage("Təlim formatı düzgün deyil.");
        RuleFor(x => x.Description).NotEmpty().WithMessage("Təsvir boş ola bilməz.").MaximumLength(2000);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).WithMessage("Qiymət mənfi ola bilməz.");
        RuleFor(x => x.DurationHours).GreaterThan(0).WithMessage("Müddət 0-dan böyük olmalıdır.");
        RuleFor(x => x.MetaLabel).NotEmpty().WithMessage("Meta etiket boş ola bilməz.").MaximumLength(100);

        RuleFor(x => x.SeatLimit)
            .GreaterThan(0).When(x => x.SeatLimit.HasValue)
            .WithMessage("Yer limiti 0-dan böyük olmalıdır.");

        RuleFor(x => x.SeatLimit)
            .NotNull().When(x => x.Format == TrainingFormat.Live)
            .WithMessage("Canlı təlim üçün yer limiti göstərilməlidir.");

        RuleForEach(x => x.Lessons).SetValidator(new CreateTrainingLessonRequestValidator());
    }
}

public sealed class AddCartItemRequestValidator : AbstractValidator<AddCartItemRequest>
{
    public AddCartItemRequestValidator()
    {
        RuleFor(x => x.ItemType).IsInEnum().WithMessage("Məhsul növü düzgün deyil.");
        RuleFor(x => x.ItemId).NotEmpty().WithMessage("Məhsul id-si boş ola bilməz.");
    }
}

public sealed class CreateQuizRequestValidator : AbstractValidator<CreateQuizRequest>
{
    public CreateQuizRequestValidator()
    {
        RuleFor(x => x.PassPercent)
            .InclusiveBetween(1, 100).WithMessage("Keçid balı 1 ilə 100 arasında olmalıdır.");

        RuleFor(x => x.Mode)
            .Must(m => m is "append" or "replace")
            .WithMessage("Mode yalnız \"append\" və ya \"replace\" ola bilər.");

        RuleFor(x => x.Questions)
            .NotEmpty().WithMessage("Ən azı bir sual göndərilməlidir.");

        RuleForEach(x => x.Questions).SetValidator(new CreateQuizQuestionRequestValidator());
    }
}

public sealed class CreateQuizQuestionRequestValidator : AbstractValidator<CreateQuizQuestionRequest>
{
    public CreateQuizQuestionRequestValidator()
    {
        RuleFor(x => x.QuestionText)
            .NotEmpty().WithMessage("Sual mətni boş ola bilməz.")
            .MaximumLength(500);

        RuleFor(x => x.Options)
            .Must(o => o.Count >= 2).WithMessage("Hər sualda ən azı 2 variant olmalıdır.")
            .Must(o => o.All(x => !string.IsNullOrWhiteSpace(x))).WithMessage("Variantlar boş ola bilməz.");

        RuleFor(x => x)
            .Must(q => q.CorrectOptionIndex >= 0 && q.CorrectOptionIndex < q.Options.Count)
            .WithMessage("Düzgün variantın indeksi variantlar sırasından kənardadır.")
            .OverridePropertyName(nameof(CreateQuizQuestionRequest.CorrectOptionIndex));
    }
}

public sealed class SubmitQuizRequestValidator : AbstractValidator<SubmitQuizRequest>
{
    public SubmitQuizRequestValidator()
    {
        RuleFor(x => x.Answers).NotEmpty().WithMessage("Cavablar boş ola bilməz.");

        RuleForEach(x => x.Answers).ChildRules(a =>
        {
            a.RuleFor(x => x.QuestionId).NotEmpty().WithMessage("Sual id-si boş ola bilməz.");
            a.RuleFor(x => x.SelectedIndex).GreaterThanOrEqualTo(0).WithMessage("Seçilmiş variant düzgün deyil.");
        });
    }
}

public sealed class IssueCertificateRequestValidator : AbstractValidator<IssueCertificateRequest>
{
    public IssueCertificateRequestValidator()
    {
        RuleFor(x => x.UserFullName).NotEmpty().WithMessage("İstifadəçinin adı boş ola bilməz.").MaximumLength(150);
        RuleFor(x => x.TrainingId).NotEmpty().WithMessage("Təlim seçilməlidir.");
    }
}

public sealed class SaveBlogPostRequestValidator : AbstractValidator<SaveBlogPostRequest>
{
    public SaveBlogPostRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Başlıq boş ola bilməz.").MaximumLength(250);
        RuleFor(x => x.Tag).NotEmpty().WithMessage("Etiket boş ola bilməz.").MaximumLength(60);
        RuleFor(x => x.ReadTime).NotEmpty().WithMessage("Oxunma müddəti boş ola bilməz.").MaximumLength(20);
        RuleFor(x => x.Excerpt).NotEmpty().WithMessage("Qısa təsvir boş ola bilməz.").MaximumLength(600);

        RuleFor(x => x.Body)
            .Must(b => b.Any(p => !string.IsNullOrWhiteSpace(p)))
            .WithMessage("Ən azı bir paraqraf olmalıdır.");
    }
}

public sealed class CreateTrainingLessonRequestValidator : AbstractValidator<CreateTrainingLessonRequest>
{
    public CreateTrainingLessonRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Dərsin başlığı boş ola bilməz.").MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().WithMessage("Dərsin təsviri boş ola bilməz.").MaximumLength(2000);

        RuleFor(x => x.VideoUrl)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri)
                         && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .When(x => !string.IsNullOrWhiteSpace(x.VideoUrl))
            .WithMessage("Video linki tam URL olmalıdır (http:// və ya https:// ilə).");

        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(1, 600).When(x => x.DurationMinutes.HasValue)
            .WithMessage("Dərsin müddəti 1 ilə 600 dəqiqə arasında olmalıdır.");
    }
}

public sealed class SaveTrainingLessonsRequestValidator : AbstractValidator<SaveTrainingLessonsRequest>
{
    public SaveTrainingLessonsRequestValidator()
    {
        RuleFor(x => x.Mode)
            .Must(m => m is "append" or "replace")
            .WithMessage("Mode yalnız \"append\" və ya \"replace\" ola bilər.");

        RuleFor(x => x.Lessons).NotEmpty().WithMessage("Ən azı bir dərs göndərilməlidir.");
        RuleForEach(x => x.Lessons).SetValidator(new CreateTrainingLessonRequestValidator());
    }
}
