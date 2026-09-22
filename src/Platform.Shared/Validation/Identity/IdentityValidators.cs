using FluentValidation;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Identity;
using Platform.Shared.Entities.Identity;
using Platform.Shared.Validation.Common;

namespace Platform.Shared.Validation.Identity;

/// <summary>
/// Rules for the role fields common to create and update.
/// </summary>
internal sealed class RoleFieldsValidator : AbstractValidator<IRoleFields>
{
    /// <summary>
    /// Defines the shared role rules.
    /// </summary>
    public RoleFieldsValidator()
    {
        RuleFor(x => x.Name).ValidName();
        RuleFor(x => x.Capabilities)
            .NotEmpty().WithMessage("Choose at least one capability.")
            .Must(codes => codes.All(Capabilities.IsKnown)).WithMessage("Unknown capability code.")
            .Must(codes => codes.Distinct().Count() == codes.Count).WithMessage("A capability is listed twice.");
    }
}

/// <summary>
/// Validates <see cref="CreateRoleRequest"/>.
/// </summary>
public class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    /// <summary>
    /// Applies the shared role rules.
    /// </summary>
    public CreateRoleRequestValidator() => Include(new RoleFieldsValidator());
}

/// <summary>
/// Validates <see cref="UpdateRoleRequest"/>.
/// </summary>
public class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleRequest>
{
    /// <summary>
    /// Applies the shared role rules.
    /// </summary>
    public UpdateRoleRequestValidator() => Include(new RoleFieldsValidator());
}

/// <summary>
/// Validates one scope grant: global has no id, every other type needs one.
/// </summary>
public class ScopeGrantDtoValidator : AbstractValidator<ScopeGrantDto>
{
    /// <summary>
    /// Defines the scope rules.
    /// </summary>
    public ScopeGrantDtoValidator()
    {
        RuleFor(x => x.ScopeType).IsInEnum();
        RuleFor(x => x.ScopeId)
            .Null().When(x => x.ScopeType == ScopeType.Global).WithMessage("A global scope has no scope id.")
            .NotEmpty().When(x => x.ScopeType != ScopeType.Global).WithMessage("Choose which {PropertyName} this scope covers.");
    }
}

/// <summary>
/// Rules for the user fields common to create and update.
/// </summary>
internal sealed class UserFieldsValidator : AbstractValidator<IUserFields>
{
    /// <summary>
    /// Defines the shared user rules.
    /// </summary>
    public UserFieldsValidator()
    {
        RuleFor(x => x.Name).ValidName();
        RuleFor(x => x.Phone).ValidOptionalPhone();
        RuleFor(x => x.RoleIds)
            .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("A role is listed twice.");
        RuleForEach(x => x.Scopes).SetValidator(new ScopeGrantDtoValidator());
    }
}

/// <summary>
/// Validates <see cref="CreateUserRequest"/>.
/// </summary>
public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    /// <summary>
    /// Applies the shared user rules plus the sign-in fields.
    /// </summary>
    public CreateUserRequestValidator()
    {
        Include(new UserFieldsValidator());
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.Password).ValidPassword();
    }
}

/// <summary>
/// Validates <see cref="UpdateUserRequest"/>.
/// </summary>
public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    /// <summary>
    /// Applies the shared user rules.
    /// </summary>
    public UpdateUserRequestValidator() => Include(new UserFieldsValidator());
}
