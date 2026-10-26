namespace BugBoard.Api.Services;

public enum ServiceError
{
    None,
    NotFound,    // -> 404
    Forbidden,   // -> 403
    Conflict,    // -> 409
    Validation   // -> 400
}

/// <summary>
/// What a service returns instead of throwing exceptions for "expected" failures.
/// The service decides WHAT went wrong; the controller decides which HTTP status that maps to.
/// </summary>
public class ServiceResult
{
    public ServiceError Error { get; protected init; }
    public string? Message { get; protected init; }
    public IReadOnlyDictionary<string, string[]>? FieldErrors { get; protected init; }
    public bool Succeeded => Error == ServiceError.None;

    public static ServiceResult Ok() => new();
    public static ServiceResult NotFound(string message) => new() { Error = ServiceError.NotFound, Message = message };
    public static ServiceResult Forbidden(string message) => new() { Error = ServiceError.Forbidden, Message = message };
    public static ServiceResult Conflict(string message) => new() { Error = ServiceError.Conflict, Message = message };
    public static ServiceResult Invalid(string field, string message) => new()
    {
        Error = ServiceError.Validation,
        Message = message,
        FieldErrors = new Dictionary<string, string[]> { [field] = [message] }
    };
}

public sealed class ServiceResult<T> : ServiceResult
{
    public T? Value { get; private init; }

    public static ServiceResult<T> Ok(T value) => new() { Value = value };
    public new static ServiceResult<T> NotFound(string message) => new() { Error = ServiceError.NotFound, Message = message };
    public new static ServiceResult<T> Forbidden(string message) => new() { Error = ServiceError.Forbidden, Message = message };
    public new static ServiceResult<T> Conflict(string message) => new() { Error = ServiceError.Conflict, Message = message };
    public new static ServiceResult<T> Invalid(string field, string message) => new()
    {
        Error = ServiceError.Validation,
        Message = message,
        FieldErrors = new Dictionary<string, string[]> { [field] = [message] }
    };
}
