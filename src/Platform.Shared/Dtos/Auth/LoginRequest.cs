namespace Platform.Shared.Dtos.Auth;

/// <summary>
/// Credentials submitted to <c>POST /api/auth/login</c>.
/// </summary>
public class LoginRequest
{
    /// <summary>Login email.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Plain-text password; only ever sent over HTTPS.</summary>
    public string Password { get; set; } = string.Empty;
}
