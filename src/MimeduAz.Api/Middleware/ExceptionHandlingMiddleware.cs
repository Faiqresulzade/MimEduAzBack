using System.Text.Json;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Contracts.Common;

namespace MimeduAz.Api.Middleware;

/// <summary>
/// Bütün idarə olunmayan xətaları tutur və vahid <see cref="ApiErrorResponse"/> formatına çevirir.
/// Şəxsi/həssas məlumat loglanmır - yalnız xəta tipi və trace id.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await WriteResponseAsync(context, ex);
        }
    }

    private async Task WriteResponseAsync(HttpContext context, Exception exception)
    {
        var traceId = context.TraceIdentifier;

        var response = exception switch
        {
            ValidationFailedException v => new ApiErrorResponse
            {
                StatusCode = v.StatusCode,
                Message = v.Message,
                Errors = v.Errors,
                TraceId = traceId
            },
            AppException app => new ApiErrorResponse
            {
                StatusCode = app.StatusCode,
                Message = app.Message,
                TraceId = traceId
            },
            _ => new ApiErrorResponse
            {
                StatusCode = StatusCodes.Status500InternalServerError,
                Message = _environment.IsDevelopment()
                    ? exception.Message
                    : "Serverdə gözlənilməz xəta baş verdi.",
                TraceId = traceId
            }
        };

        if (response.StatusCode >= 500)
        {
            _logger.LogError(exception, "İdarə olunmayan xəta. TraceId: {TraceId}", traceId);
        }
        else
        {
            _logger.LogWarning(
                "Biznes qaydası pozuldu: {ExceptionType}. TraceId: {TraceId}",
                exception.GetType().Name, traceId);
        }

        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = response.StatusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        await context.Response.WriteAsync(json);
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionHandlingMiddleware>();
}
