namespace MimeduAz.Domain.Entities;

/// <summary>
/// Sınaq sualı. Mətn və şəkil bir-birini əvəz etmir, tamamlayır:
/// sadə sual yalnız mətn, düsturlu/qrafikli sual isə şəkil (və opsional izah mətni) ola bilər.
/// </summary>
public class ExamQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SectionId { get; set; }
    public ExamSection? Section { get; set; }

    public int OrderIndex { get; set; }

    /// <summary>
    /// Sualın mətni. LaTeX ifadələri ($...$) saxlanıla bilər - render frontend tərəfdədir.
    /// Sual tamamilə şəkildən ibarətdirsə boş qala bilər.
    /// </summary>
    public string QuestionText { get; set; } = string.Empty;

    /// <summary>
    /// Sual şəklinin fayl saxlayıcısındakı nisbi yolu. Düstur, qrafik və cədvəl olan
    /// suallar üçün - müəllim şəkli yükləyir, variantlar A/B/C/D kimi qalır.
    /// </summary>
    public string? ImagePath { get; set; }

    /// <summary>Cavab variantları. LaTeX ifadəsi saxlaya bilər.</summary>
    public List<string> Options { get; set; } = new();

    /// <summary>Düzgün variantın <see cref="Options"/> içindəki indeksi. Sınaq gedərkən API cavabında HEÇ VAXT göndərilmir.</summary>
    public int CorrectOptionIndex { get; set; }

    public ICollection<ExamAnswer> Answers { get; set; } = new List<ExamAnswer>();
}
