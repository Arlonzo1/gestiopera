namespace BillaresPos.Application.Common.Exceptions;

public abstract class AppException(string message) : Exception(message)
{
    public virtual int StatusCode => 500;
}

public sealed class NotFoundException(string message) : AppException(message)
{
    public override int StatusCode => 404;
}

public sealed class ForbiddenException(string message) : AppException(message)
{
    public override int StatusCode => 403;
}

public sealed class ValidationAppException(string message, IReadOnlyDictionary<string, string[]> errors) : AppException(message)
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
    public override int StatusCode => 400;
}
