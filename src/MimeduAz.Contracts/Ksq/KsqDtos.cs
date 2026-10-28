namespace MimeduAz.Contracts.Ksq;

/// <summary>Müəllimin saxlanmış KSQ testi (açar A/B + ballar).</summary>
public sealed record KsqTestDto(
    Guid Id,
    string Name,
    IReadOnlyList<int> ClosedPoints,
    IReadOnlyList<int> OpenPoints,
    IReadOnlyList<string?> KeyA,
    IReadOnlyList<string?> KeyB,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Test yaratmaq və yeniləmək üçün eyni gövdə.</summary>
public sealed class SaveKsqTestRequest
{
    public string Name { get; set; } = string.Empty;
    public List<int> ClosedPoints { get; set; } = new();
    public List<int> OpenPoints { get; set; } = new();
    public List<string?> KeyA { get; set; } = new();
    public List<string?> KeyB { get; set; } = new();
}

/// <summary>
/// Yoxlanmış kartın nəticəsi. Sahə adları KSQ alətinin orijinal qeyd formatı ilə eynidir:
/// <c>id, test, testId, sinif, ad, variant, kod, cavablar, aciq, bal, max, sehv, ts</c>.
/// </summary>
public sealed record KsqResultDto(
    Guid Id,
    string Test,
    Guid TestId,
    string Sinif,
    string Ad,
    string Variant,
    string Kod,
    IReadOnlyList<string?> Cavablar,
    IReadOnlyList<decimal> Aciq,
    decimal Bal,
    decimal Max,
    IReadOnlyList<int> Sehv,
    DateTime Ts);

/// <summary>Nəticələr siyahısı + filtr üçün mövcud siniflər (sinif filtrindən asılı olmadan).</summary>
public sealed record KsqResultListDto(
    IReadOnlyList<KsqResultDto> Items,
    IReadOnlyList<string> Classes);

/// <summary>
/// Nəticəni saxlamaq üçün gövdə. Bal göndərilmir — server açara görə özü hesablayır.
/// </summary>
public sealed class SaveKsqResultRequest
{
    public Guid TestId { get; set; }
    public string? Sinif { get; set; }
    public string? Ad { get; set; }
    public string Variant { get; set; } = "A";
    public string? Kod { get; set; }
    public List<string?> Cavablar { get; set; } = new();
    public List<decimal?> Aciq { get; set; } = new();
}

/// <summary>"Hamısını saxla" düyməsi üçün.</summary>
public sealed class SaveKsqResultsBulkRequest
{
    public List<SaveKsqResultRequest> Items { get; set; } = new();
}

/// <summary>
/// AI-ın şəkildən oxuduğu kart. Şəkil özü heç yerdə saxlanmır.
/// <c>aciqCavablar</c> — açıq suallara yazılmış mətn (müəllimə ipucu), bal deyil.
/// </summary>
public sealed record KsqScanResultDto(
    string? Ad,
    string? Variant,
    string? Kod,
    IReadOnlyList<string?> Cavablar,
    IReadOnlyList<string?> AciqCavablar,
    IReadOnlyList<int> Unsure,
    string? Qeyd);
