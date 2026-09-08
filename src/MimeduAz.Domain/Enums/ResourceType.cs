namespace MimeduAz.Domain.Enums;

/// <summary>Resurs Bankındakı material növü.</summary>
public enum ResourceType
{
    WorkSheet = 0,
    Presentation = 1,
    Test = 2,
    MethodGuide = 3,

    /// <summary>
    /// Video dərs — fayl yüklənmir, YouTube/Vimeo linki saxlanılır.
    /// Ödənişli ola bilər; bu halda link yalnız satın alandan sonra açılır.
    /// </summary>
    Video = 4,

    /// <summary>
    /// Başqa saytda yaradılmış material (Wordwall, LearningApps, Canva, Google Drive...).
    /// Yalnız pulsuz ola bilər — link paylaşıldıqdan sonra ona nəzarət mümkün deyil.
    /// </summary>
    ExternalLink = 5
}
