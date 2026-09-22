namespace Platform.Api.Common.Exceptions;

/// <summary>
/// Base of every expected, user-facing error. The global exception handler
/// turns it into a ProblemDetails response with <see cref="StatusCode"/>.
/// Anything that is not an <see cref="AppException"/> becomes a 500.
/// </summary>
public abstract class AppException : Exception
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="message">Safe-to-show message returned to the caller.</param>
    protected AppException(string message) : base(message)
    {
    }

    /// <summary>HTTP status code returned to the caller.</summary>
    public abstract int StatusCode { get; }

    /// <summary>Short title for the ProblemDetails response.</summary>
    public abstract string Title { get; }

    /// <summary>
    /// Optional per-field errors, keyed by property name. When set, the response is
    /// a ValidationProblemDetails and the UI shows each message next to its field.
    /// </summary>
    public virtual IDictionary<string, string[]>? FieldErrors => null;
}

/// <summary>
/// A value that must be unique is already used by another record. Firestore
/// has no unique indexes, so services raise this after an existence check.
/// </summary>
public sealed class ConflictException : AppException
{
    private readonly string _field;

    /// <summary>
    /// Creates the exception for one duplicated field.
    /// </summary>
    /// <param name="field">C# property name of the duplicated field.</param>
    /// <param name="message">Safe-to-show message.</param>
    public ConflictException(string field, string message) : base(message)
    {
        _field = field;
    }

    /// <inheritdoc />
    public override int StatusCode => StatusCodes.Status409Conflict;

    /// <inheritdoc />
    public override string Title => "Duplicate";

    /// <inheritdoc />
    public override IDictionary<string, string[]> FieldErrors =>
        new Dictionary<string, string[]> { [_field] = new[] { Message } };
}

/// <summary>
/// The record does not exist, or exists outside the caller's scope. Both
/// cases return 404 so the response never confirms that a record exists (P6).
/// </summary>
public sealed class NotFoundException : AppException
{
    /// <summary>
    /// Creates the exception for a missing record.
    /// </summary>
    /// <param name="entityName">Human name of the entity, e.g. "Brand".</param>
    public NotFoundException(string entityName) : base($"{entityName} was not found.")
    {
    }

    /// <inheritdoc />
    public override int StatusCode => StatusCodes.Status404NotFound;

    /// <inheritdoc />
    public override string Title => "Not found";
}

/// <summary>
/// The request is well-formed but breaks a business rule.
/// </summary>
public sealed class BusinessRuleException : AppException
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="message">Which rule was broken, safe to show the user.</param>
    public BusinessRuleException(string message) : base(message)
    {
    }

    /// <inheritdoc />
    public override int StatusCode => StatusCodes.Status422UnprocessableEntity;

    /// <inheritdoc />
    public override string Title => "Business rule violated";
}

/// <summary>
/// Credentials were missing or wrong. The message never says which.
/// </summary>
public sealed class AuthenticationFailedException : AppException
{
    /// <summary>
    /// Creates the exception with the standard, non-revealing message.
    /// </summary>
    public AuthenticationFailedException() : base("Email or password is incorrect.")
    {
    }

    /// <inheritdoc />
    public override int StatusCode => StatusCodes.Status401Unauthorized;

    /// <inheritdoc />
    public override string Title => "Authentication failed";
}
