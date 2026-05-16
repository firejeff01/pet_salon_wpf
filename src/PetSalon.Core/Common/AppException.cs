namespace PetSalon.Core.Common;

public enum AppErrorKind
{
    Validation,
    NotFound,
    Conflict,
    Unprocessable,
    Internal,
}

public sealed class AppException : Exception
{
    public AppErrorKind Kind { get; }
    public string Code { get; }
    public object? Details { get; }

    public AppException(AppErrorKind kind, string code, string message, object? details = null)
        : base(message)
    {
        Kind = kind;
        Code = code;
        Details = details;
    }

    public static AppException Validation(string message, object? details = null)
        => new(AppErrorKind.Validation, "VALIDATION_ERROR", message, details);

    public static AppException NotFound(string code, string message)
        => new(AppErrorKind.NotFound, code, message);

    public static AppException Conflict(string code, string message, object? details = null)
        => new(AppErrorKind.Conflict, code, message, details);

    public static AppException Unprocessable(string code, string message, object? details = null)
        => new(AppErrorKind.Unprocessable, code, message, details);
}
