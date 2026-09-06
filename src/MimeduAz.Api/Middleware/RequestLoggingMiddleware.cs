using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Options;
using MimeduAz.Application.Common.Utilities;
using MimeduAz.Domain.Entities;

namespace MimeduAz.Api.Middleware;

/// <summary>
/// Hər HTTP sorğusunu və cavabını audit cədvəlinə yazır.
/// Şifrə/token kimi həssas sahələr <see cref="SensitiveDataRedactor"/> ilə maskalanır,
/// fayl yükləmələrinin (multipart) gövdəsi isə ümumiyyətlə oxunmur.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private const string MultipartPrefix = "multipart/";

    private readonly RequestDelegate _next;
    private readonly IRequestLogSink _sink;
    private readonly RequestLogOptions _options;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        IRequestLogSink sink,
        IOptions<RequestLogOptions> options,
        ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _sink = sink;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentUserService currentUser)
    {
        if (!_options.Enabled || IsExcluded(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var requestBody = await ReadRequestBodyAsync(context.Request);

        var originalResponseBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        string? exceptionType = null;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // Exception yuxarıdakı ExceptionHandlingMiddleware-də tutulacaq;
            // biz sadəcə audit qeydində onu işarələyirik.
            exceptionType = ex.GetType().Name;
            throw;
        }
        finally
        {
            stopwatch.Stop();

            string? responseBody = null;
            try
            {
                buffer.Position = 0;
                responseBody = await ReadResponseBodyAsync(buffer, context.Response.ContentType);

                buffer.Position = 0;
                await buffer.CopyToAsync(originalResponseBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audit log üçün cavab gövdəsi oxuna bilmədi.");
            }
            finally
            {
                context.Response.Body = originalResponseBody;
            }

            Record(context, currentUser, requestBody, responseBody, exceptionType, stopwatch.ElapsedMilliseconds);
        }
    }

    private bool IsExcluded(PathString path) =>
        _options.ExcludedPathPrefixes.Any(prefix =>
            path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));

    private async Task<string?> ReadRequestBodyAsync(HttpRequest request)
    {
        if (!_options.LogRequestBody || request.ContentLength is null or 0)
        {
            return null;
        }

        var contentType = request.ContentType ?? string.Empty;

        // Fayl yükləmələri 25 MB-a qədər ola bilər - gövdəni yaddaşa oxumuruq.
        if (contentType.StartsWith(MultipartPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return $"[{contentType}, {request.ContentLength} bayt - gövdə loglanmadı]";
        }

        try
        {
            // Gövdəni oxuyub geri sarımaq üçün buferləmə lazımdır.
            request.EnableBuffering();

            using var reader = new StreamReader(
                request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);

            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0;

            return SensitiveDataRedactor.Redact(body, _options.MaxBodyLength);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audit log üçün sorğu gövdəsi oxuna bilmədi.");
            return null;
        }
    }

    private async Task<string?> ReadResponseBodyAsync(MemoryStream buffer, string? contentType)
    {
        if (!_options.LogResponseBody || buffer.Length == 0)
        {
            return null;
        }

        // Yalnız JSON cavablar loglanır; fayl/binary məzmun buraxılır.
        if (contentType is null || !contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return $"[{contentType ?? "naməlum tip"}, {buffer.Length} bayt - gövdə loglanmadı]";
        }

        using var reader = new StreamReader(buffer, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();

        return SensitiveDataRedactor.Redact(body, _options.MaxBodyLength);
    }

    private void Record(
        HttpContext context,
        ICurrentUserService currentUser,
        string? requestBody,
        string? responseBody,
        string? exceptionType,
        long durationMs)
    {
        try
        {
            var log = new RequestLog
            {
                TraceId = context.TraceIdentifier,
                Method = context.Request.Method,
                Path = SensitiveDataRedactor.Truncate(context.Request.Path.Value, 500) ?? "/",
                QueryString = SensitiveDataRedactor.Truncate(
                    context.Request.QueryString.HasValue ? context.Request.QueryString.Value : null, 1000),
                StatusCode = exceptionType is null ? context.Response.StatusCode : StatusCodes.Status500InternalServerError,
                DurationMs = (int)Math.Min(durationMs, int.MaxValue),
                UserId = currentUser.UserId,
                UserEmail = SensitiveDataRedactor.Truncate(currentUser.Email, 256),
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = SensitiveDataRedactor.Truncate(
                    context.Request.Headers.UserAgent.ToString(), 512),
                RequestContentType = SensitiveDataRedactor.Truncate(context.Request.ContentType, 150),
                RequestBody = requestBody,
                ResponseBody = responseBody,
                ExceptionType = exceptionType,
                CreatedAt = DateTime.UtcNow
            };

            if (!_sink.TryEnqueue(log))
            {
                _logger.LogWarning("Audit log növbəsi doludur - qeyd atıldı. TraceId: {TraceId}", log.TraceId);
            }
        }
        catch (Exception ex)
        {
            // Loglama heç bir halda sorğunu pozmamalıdır.
            _logger.LogError(ex, "Audit qeydi yaradıla bilmədi.");
        }
    }
}

public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app) =>
        app.UseMiddleware<RequestLoggingMiddleware>();
}
