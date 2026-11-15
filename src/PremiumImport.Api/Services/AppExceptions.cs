namespace PremiumImport.Api.Services;

/// <summary>Business errors thrown by services and translated to HTTP responses in one place (AppExceptionHandler).</summary>
public abstract class AppException(string message) : Exception(message)
{
    public abstract int StatusCode { get; }
}

public sealed class NotFoundException(string message) : AppException(message)
{
    public override int StatusCode => StatusCodes.Status404NotFound;
}

public sealed class ConflictException(string message) : AppException(message)
{
    public override int StatusCode => StatusCodes.Status409Conflict;
}

public sealed class BadInputException(string message) : AppException(message)
{
    public override int StatusCode => StatusCodes.Status400BadRequest;
}
