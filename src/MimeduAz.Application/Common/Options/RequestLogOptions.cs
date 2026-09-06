namespace MimeduAz.Application.Common.Options;

/// <summary>HTTP audit log tənzimləmələri. appsettings.json -> "RequestLog".</summary>
public sealed class RequestLogOptions
{
    public const string SectionName = "RequestLog";

    public bool Enabled { get; set; } = true;

    public bool LogRequestBody { get; set; } = true;
    public bool LogResponseBody { get; set; } = true;

    /// <summary>Gövdələr bu uzunluqdan sonra kəsilir (simvol sayı).</summary>
    public int MaxBodyLength { get; set; } = 4000;

    /// <summary>Bu prefikslərlə başlayan yollar loglanmır.</summary>
    public string[] ExcludedPathPrefixes { get; set; } =
        { "/swagger", "/uploads", "/health", "/favicon.ico" };

    /// <summary>
    /// Yaddaşdakı növbənin tutumu. Dolarsa ən köhnə qeydlər atılır -
    /// loglama heç vaxt sorğunu ləngitməməli və ya pozmamalıdır.
    /// </summary>
    public int QueueCapacity { get; set; } = 2000;

    /// <summary>Bazaya bir dəfəyə yazılan qeyd sayı.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>Batch dolmasa belə bu qədər saniyədən sonra yazılır.</summary>
    public int FlushIntervalSeconds { get; set; } = 5;

    /// <summary>Bu gündən köhnə qeydlər avtomatik silinir. 0 = silmə.</summary>
    public int RetentionDays { get; set; } = 30;
}
