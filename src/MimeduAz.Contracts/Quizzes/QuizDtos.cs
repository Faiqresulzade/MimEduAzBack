namespace MimeduAz.Contracts.Quizzes;

/// <summary>Quiz metadata-sı (sualsız).</summary>
public sealed record QuizDto(
    Guid Id,
    Guid ResourceId,
    string ResourceName,
    int PassPercent,
    int QuestionCount,
    DateTime CreatedAt);

/// <summary>İmtahan sualı — düzgün cavabın indeksi HEÇ VAXT daxil edilmir.</summary>
public sealed record QuizQuestionDto(
    Guid Id,
    int OrderIndex,
    string QuestionText,
    IReadOnlyList<string> Options);

public sealed class CreateQuizQuestionRequest
{
    public string QuestionText { get; set; } = string.Empty;
    public List<string> Options { get; set; } = new();
    public int CorrectOptionIndex { get; set; }
}

/// <summary>Quiz yaratma/əlavəetmə. <see cref="Mode"/> = "append" | "replace".</summary>
public sealed class CreateQuizRequest
{
    public int PassPercent { get; set; } = 70;
    public string Mode { get; set; } = "append";
    public List<CreateQuizQuestionRequest> Questions { get; set; } = new();
}

public sealed record QuizAnswerDto(Guid QuestionId, int SelectedIndex);

public sealed class SubmitQuizRequest
{
    public List<QuizAnswerDto> Answers { get; set; } = new();
}

public sealed record QuizResultDto(
    Guid AttemptId,
    int ScorePercent,
    int CorrectCount,
    int TotalQuestions,
    int PassPercent,
    bool Passed,
    string? CertificateCode);
