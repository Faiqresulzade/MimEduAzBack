using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Options;
using MimeduAz.Contracts.Ksq;

namespace MimeduAz.Infrastructure.Ai;

/// <summary>
/// Cavab kartını OpenRouter-dəki vision model ilə oxuyur.
/// Şəkil yalnız sorğu gövdəsində (base64) gedir; heç yerdə saxlanmır və loglanmır —
/// loga yalnız status kodu, model adı və müddət yazılır.
/// </summary>
public sealed class OpenRouterCardReader : IKsqCardReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly OpenRouterOptions _options;
    private readonly ILogger<OpenRouterCardReader> _logger;

    public OpenRouterCardReader(
        HttpClient http,
        IOptions<OpenRouterOptions> options,
        ILogger<OpenRouterCardReader> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<KsqScanResultDto> ReadAsync(
        byte[] image, string contentType, int closedCount, int openCount, CancellationToken ct)
    {
        if (!_options.IsConfigured)
        {
            throw ExternalServiceException.Unavailable(
                "Avtomatik oxuma hələ qurulmayıb. Cavabları əl ilə qeyd edin.");
        }

        var body = new ChatRequest(
            _options.VisionModel,
            new[]
            {
                new ChatMessage("user", new object[]
                {
                    new TextPart("text", KsqCardPrompt.Build(closedCount, openCount)),
                    new ImagePart("image_url", new ImageUrl($"data:{contentType};base64,{Convert.ToBase64String(image)}"))
                })
            },
            Temperature: 0,
            MaxTokens: 4000,
            ResponseFormat: new ResponseFormat("json_object"));

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Headers.TryAddWithoutValidation("HTTP-Referer", _options.SiteUrl);
        request.Headers.TryAddWithoutValidation("X-Title", _options.AppName);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        var started = DateTime.UtcNow;
        string payload;

        try
        {
            using var response = await _http.SendAsync(request, timeout.Token);
            payload = await response.Content.ReadAsStringAsync(timeout.Token);

            _logger.LogInformation(
                "KSQ kartı oxundu: model {Model}, status {Status}, {Elapsed} ms",
                _options.VisionModel, (int)response.StatusCode, (int)(DateTime.UtcNow - started).TotalMilliseconds);

            if (!response.IsSuccessStatusCode)
            {
                throw UpstreamError((int)response.StatusCode, payload);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("KSQ kartı {Seconds} saniyəyə oxunmadı.", _options.TimeoutSeconds);
            throw ExternalServiceException.Timeout(
                "Şəkil vaxtında oxunmadı. Yenidən cəhd edin və ya cavabları əl ilə qeyd edin.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("OpenRouter-ə qoşulmaq alınmadı: {Error}", ex.Message);
            throw ExternalServiceException.BadGateway(
                "AI xidmətinə qoşulmaq alınmadı. Bir az sonra yenidən cəhd edin.");
        }

        var content = ExtractContent(payload);
        var result = KsqCardResponseParser.TryParse(content, closedCount, openCount);

        if (result is null)
        {
            _logger.LogWarning("KSQ: modelin cavabı JSON kimi oxunmadı (uzunluq {Length}).", content?.Length ?? 0);
            throw ExternalServiceException.BadGateway(
                "Kart oxunmadı — AI cavabı anlaşılmadı. Şəkli daha aydın çəkib yenidən yoxlayın.");
        }

        return result;
    }

    private ExternalServiceException UpstreamError(int status, string payload)
    {
        var message = TryReadErrorMessage(payload);
        _logger.LogWarning("OpenRouter xətası {Status}: {Message}", status, message ?? "(mesaj yoxdur)");

        return status switch
        {
            401 or 403 => ExternalServiceException.Unavailable(
                "AI xidmətinin açarı etibarsızdır. Administratora müraciət edin."),
            402 => ExternalServiceException.Unavailable(
                "AI xidmətinin balansı bitib. Administratora müraciət edin."),
            429 => ExternalServiceException.BadGateway(
                "AI xidməti hazırda çox yüklüdür. Bir az sonra yenidən cəhd edin."),
            _ => ExternalServiceException.BadGateway(
                "AI xidməti xəta qaytardı. Yenidən cəhd edin.")
        };
    }

    private static string? ExtractContent(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var choices = document.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() == 0)
            {
                return null;
            }

            var content = choices[0].GetProperty("message").GetProperty("content");
            return content.ValueKind == JsonValueKind.String ? content.GetString() : null;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    private static string? TryReadErrorMessage(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.TryGetProperty("error", out var error)
                   && error.TryGetProperty("message", out var message)
                ? message.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record ChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages,
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("response_format")] ResponseFormat ResponseFormat);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] IReadOnlyList<object> Content);

    private sealed record TextPart(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string Text);

    private sealed record ImagePart(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("image_url")] ImageUrl ImageUrl);

    private sealed record ImageUrl([property: JsonPropertyName("url")] string Url);

    private sealed record ResponseFormat([property: JsonPropertyName("type")] string Type);
}
