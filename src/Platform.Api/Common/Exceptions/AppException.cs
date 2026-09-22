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
