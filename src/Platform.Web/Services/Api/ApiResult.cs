using System.Net;

namespace Platform.Web.Services.Api;

/// <summary>
/// Outcome of an API call that returns no body. The UI never sees an
/// <c>HttpResponseMessage</c>; it sees success, or a readable error.
/// </summary>
public class ApiResult
{
    /// <summary>HTTP status returned by the API.</summary>
    public HttpStatusCode StatusCode { get; init; }

    /// <summary>True for any 2xx status.</summary>
    public bool IsSuccess => (int)StatusCode is >= 200 and < 300;

    /// <summary>True when the token is missing or expired and the user must sign in again.</summary>
    public bool IsUnauthorized => StatusCode == HttpStatusCode.Unauthorized;

    /// <summary>True when the record does not exist or is out of scope.</summary>
    public bool IsNotFound => StatusCode == HttpStatusCode.NotFound;

    /// <summary>General error message from ProblemDetails, if any.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Per-field validation errors from ValidationProblemDetails, keyed by property name.</summary>
    public IDictionary<string, string[]> FieldErrors { get; init; } = new Dictionary<string, string[]>();
}

/// <summary>
/// Outcome of an API call that returns a body of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">Response body type.</typeparam>
public sealed class ApiResult<T> : ApiResult
{
    /// <summary>Deserialised response body; set only when <see cref="ApiResult.IsSuccess"/>.</summary>
    public T? Value { get; init; }
}
