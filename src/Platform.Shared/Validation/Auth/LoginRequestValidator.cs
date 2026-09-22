using FluentValidation;
using Platform.Shared.Dtos.Auth;
using Platform.Shared.Validation.Common;

namespace Platform.Shared.Validation.Auth;

/// <summary>
/// Validates login credentials before they reach the database.
/// </summary>
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    /// <summary>
    /// Defines the login rules.
    /// </summary>
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.Password).ValidPassword();
    }
}
