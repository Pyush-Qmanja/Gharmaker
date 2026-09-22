using System.Net.Http.Headers;
using Platform.Shared.Constants;

namespace Platform.Web.Services.Api;

/// <summary>
/// Adds the signed-in user's JWT to every outgoing API request. The token is
/// kept in the encrypted auth cookie, never in browser-readable storage.
/// </summary>
public sealed class BearerTokenHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="httpContextAccessor">Gives access to the signed-in user.</param>
    public BearerTokenHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Attaches <c>Authorization: Bearer ...</c> when a token is present, then sends.
    /// </summary>
    /// <param name="request">Outgoing request.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The API response.</returns>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? token = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimNames.AccessToken)?.Value;
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
