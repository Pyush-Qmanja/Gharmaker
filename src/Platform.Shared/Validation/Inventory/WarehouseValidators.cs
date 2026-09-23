using FluentValidation;
using Platform.Shared.Dtos.Inventory;
using Platform.Shared.Validation.Common;

namespace Platform.Shared.Validation.Inventory;

/// <summary>
/// Rules for the warehouse fields common to create and update.
/// </summary>
internal sealed class WarehouseFieldsValidator : AbstractValidator<IWarehouseFields>
{
    /// <summary>
    /// Defines the shared warehouse rules.
    /// </summary>
    public WarehouseFieldsValidator()
    {
        RuleFor(x => x.Code).ValidCode();
        RuleFor(x => x.Name).ValidName();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Address).NotNull().SetValidator(new AddressDtoValidator());
        RuleFor(x => x.Lat).ValidOptionalLat();
        RuleFor(x => x.Lng).ValidOptionalLng();
        RuleFor(x => x.Lng).NotNull().When(x => x.Lat.HasValue).WithMessage("Give both latitude and longitude, or neither.");
        RuleFor(x => x.Lat).NotNull().When(x => x.Lng.HasValue).WithMessage("Give both latitude and longitude, or neither.");
    }
}

/// <summary>
/// Validates <see cref="CreateWarehouseRequest"/>.
/// </summary>
public class CreateWarehouseRequestValidator : AbstractValidator<CreateWarehouseRequest>
{
    /// <summary>
    /// Applies the shared warehouse rules.
    /// </summary>
    public CreateWarehouseRequestValidator() => Include(new WarehouseFieldsValidator());
}

/// <summary>
/// Validates <see cref="UpdateWarehouseRequest"/>.
/// </summary>
public class UpdateWarehouseRequestValidator : AbstractValidator<UpdateWarehouseRequest>
{
    /// <summary>
    /// Applies the shared warehouse rules.
    /// </summary>
    public UpdateWarehouseRequestValidator() => Include(new WarehouseFieldsValidator());
}
