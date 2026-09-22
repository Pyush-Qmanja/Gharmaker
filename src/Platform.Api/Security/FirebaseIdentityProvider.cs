using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FirebaseAdmin.Auth;
using Microsoft.Extensions.Options;
using Platform.Api.Firestore;

namespace Platform.Api.Security;

/// <summary>
/// Where passwords live and are checked. The rest of the API depends only on
/// this interface, so the identity service can change without touching sign-in logic.
/// </summary>
public interface IIdentityProvider
{
    /// <summary>
    /// Verifies an email and password.
    /// </summary>
    /// <param name="email">Normalised (trimmed, lower-case) email.</param>
    /// <param name="password">Plain-text password.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The account's uid, or null when the credentials are wrong, the account is disabled or locked.</returns>
    Task<string?> VerifyPasswordAsync(string email, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the uid of the account with this email, creating the account
    /// with the given password if none exists.
    /// </summary>
    /// <param name="email">Normalised email.</param>
    /// <param name="password">Password for a newly created account.</param>
    /// <param name="displayName">Display name for a newly created account.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The account's uid.</returns>
    Task<string> EnsureAccountAsync(string email, string password, string displayName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Firebase Authentication implementation of <see cref="IIdentityProvider"/>.
/// Password checks use the Identity Toolkit REST endpoint (the only way to
/// verify a password server-side); account management uses the Admin SDK.
/// </summary>
public sealed class FirebaseIdentityProvider : IIdentityProvider
{
    /// <summary>Production Identity Toolkit base URL.</summary>
    private const string IdentityToolkitBase = "https://identitytoolkit.googleapis.com/v1/";

    private readonly HttpClient _httpClient;
    private readonly FirebaseAuth _firebaseAuth;
    private readonly FirebaseOptions _options;
    private readonly ILogger<FirebaseIdentityProvider> _logger;

    /// <summary>
    /// Creates the provider.
    /// </summary>
    /// <param name="httpClient">Client for the Identity Toolkit REST API.</param>
    /// <param name="firebaseAuth">Admin SDK entry point for account management.</param>
    /// <param name="options">Firebase settings (API key, emulator host).</param>
    /// <param name="logger">Logs why a sign-in was refused, never the password.</param>
    public FirebaseIdentityProvider(
        HttpClient httpClient,
        FirebaseAuth firebaseAuth,
        IOptions<FirebaseOptions> options,
        ILogger<FirebaseIdentityProvider> logger)
    {
        _httpClient = httpClient;
        _firebaseAuth = firebaseAuth;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string?> VerifyPasswordAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            SignInUrl(),
            new SignInRequest(email, password, ReturnSecureToken: true),
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<SignInResponse>(cancellationToken);
            return result?.LocalId;
        }

        // Firebase says why (INVALID_LOGIN_CREDENTIALS, USER_DISABLED, TOO_MANY_ATTEMPTS...).
        // Log it for support; the caller only ever sees a generic failure.
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken);
        _logger.LogInformation("Firebase sign-in refused: {Reason}", error?.Error?.Message ?? response.StatusCode.ToString());
        return null;
    }

    /// <inheritdoc />
    public async Task<string> EnsureAccountAsync(string email, string password, string displayName, CancellationToken cancellationToken = default)
    {
        try
        {
            UserRecord existing = await _firebaseAuth.GetUserByEmailAsync(email, cancellationToken);
            return existing.Uid;
        }
        catch (FirebaseAuthException ex) when (ex.AuthErrorCode == AuthErrorCode.UserNotFound)
        {
            UserRecord created = await _firebaseAuth.CreateUserAsync(
                new UserRecordArgs { Email = email, Password = password, DisplayName = displayName, EmailVerified = false },
                cancellationToken);
            return created.Uid;
        }
    }

    /// <summary>
    /// Builds the sign-in URL for production or the emulator.
    /// </summary>
    /// <returns>The absolute URL including the API key.</returns>
    /// <exception cref="InvalidOperationException">No API key configured outside the emulator.</exception>
    private string SignInUrl()
    {
        if (_options.UseAuthEmulator)
        {
            // The emulator accepts any key.
            return $"http://{_options.AuthEmulatorHost}/identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=emulator";
        }

        if (string.IsNullOrWhiteSpace(_options.WebApiKey))
        {
            throw new InvalidOperationException("Firebase:WebApiKey is not configured.");
        }

        return $"{IdentityToolkitBase}accounts:signInWithPassword?key={Uri.EscapeDataString(_options.WebApiKey)}";
    }

    /// <summary>Body of <c>accounts:signInWithPassword</c>.</summary>
    /// <param name="Email">Email.</param>
    /// <param name="Password">Password.</param>
    /// <param name="ReturnSecureToken">Must be true for the endpoint to succeed.</param>
    private sealed record SignInRequest(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("password")] string Password,
        [property: JsonPropertyName("returnSecureToken")] bool ReturnSecureToken);

    /// <summary>Successful sign-in response; only the uid is used.</summary>
    /// <param name="LocalId">The account's uid.</param>
    private sealed record SignInResponse([property: JsonPropertyName("localId")] string LocalId);

    /// <summary>Error envelope returned by Identity Toolkit.</summary>
    /// <param name="Error">Error detail.</param>
    private sealed record ErrorResponse([property: JsonPropertyName("error")] ErrorDetail? Error);

    /// <summary>Error detail; <see cref="Message"/> is a code such as INVALID_LOGIN_CREDENTIALS.</summary>
    /// <param name="Message">Error code.</param>
    private sealed record ErrorDetail([property: JsonPropertyName("message")] string? Message);
}
