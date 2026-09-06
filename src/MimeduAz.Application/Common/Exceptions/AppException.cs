namespace MimeduAz.Application.Common.Exceptions;

/// <summary>Biznes qaydası pozulanda atılan baza exception. HTTP status kodu daşıyır.</summary>
public abstract class AppException : Exception
{
    protected AppException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}

/// <summary>404 — resurs tapılmadı.</summary>
public sealed class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message, StatusCodes.NotFound) { }

    public static NotFoundException For(string entity, object key) =>
        new($"{entity} tapılmadı (id: {key}).");
}

/// <summary>400 — sorğu biznes qaydasına uyğun deyil.</summary>
public sealed class BadRequestException : AppException
{
    public BadRequestException(string message) : base(message, StatusCodes.BadRequest) { }
}

/// <summary>401 — autentifikasiya tələb olunur.</summary>
public sealed class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "Autentifikasiya tələb olunur.")
        : base(message, StatusCodes.Unauthorized) { }
}

/// <summary>403 — istifadəçinin bu əməliyyata icazəsi yoxdur.</summary>
public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message = "Bu əməliyyat üçün icazəniz yoxdur.")
        : base(message, StatusCodes.Forbidden) { }
}

/// <summary>409 — mövcud vəziyyətlə ziddiyyət (dublikat və s.).</summary>
public sealed class ConflictException : AppException
{
    public ConflictException(string message) : base(message, StatusCodes.Conflict) { }
}

/// <summary>422/400 — sahə səviyyəsində validasiya xətaları.</summary>
public sealed class ValidationFailedException : AppException
{
    public ValidationFailedException(IDictionary<string, string[]> errors)
        : base("Göndərilən məlumatlar düzgün deyil.", StatusCodes.BadRequest)
    {
        Errors = errors;
    }

    public ValidationFailedException(string field, string error)
        : this(new Dictionary<string, string[]> { [field] = new[] { error } }) { }

    public IDictionary<string, string[]> Errors { get; }
}

internal static class StatusCodes
{
    public const int BadRequest = 400;
    public const int Unauthorized = 401;
    public const int Forbidden = 403;
    public const int NotFound = 404;
    public const int Conflict = 409;
}
