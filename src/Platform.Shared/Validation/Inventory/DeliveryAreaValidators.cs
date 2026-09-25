using FluentValidation;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Inventory;
using Platform.Shared.Validation.Common;

namespace Platform.Shared.Validation.Inventory;

/// <summary>
/// Limits on delivery lead times.
/// </summary>
internal static class LeadTimes
{
    /// <summary>Longest lead time accepted, in days.</summary>
    public const int MaxDays = 60;
}

/// <summary>
/// Validates <see cref="AddDeliveryAreasRequest"/>.
/// </summary>
public class AddDeliveryAreasRequestValidator : AbstractValidator<AddDeliveryAreasRequest>
{
    /// <summary>Creates the rules.</summary>
    public AddDeliveryAreasRequestValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty().WithMessage("Choose the warehouse that delivers.");
        RuleFor(x => x.Pincodes)
            .NotEmpty().WithMessage("Enter at least one PIN code.")
            .Must(p => p.Count <= FieldLengths.PincodesPerRequest)
            .WithMessage($"At most {FieldLengths.PincodesPerRequest} PIN codes at a time.");
        RuleForEach(x => x.Pincodes).ValidPincode().WithName("PIN code");
        RuleFor(x => x.LeadTimeDays).InclusiveBetween(0, LeadTimes.MaxDays).WithName("Delivery days");
    }
}

/// <summary>
/// Validates <see cref="UpdateDeliveryAreaRequest"/>.
/// </summary>
public class UpdateDeliveryAreaRequestValidator : AbstractValidator<UpdateDeliveryAreaRequest>
{
    /// <summary>Creates the rules.</summary>
    public UpdateDeliveryAreaRequestValidator()
    {
        RuleFor(x => x.LeadTimeDays).InclusiveBetween(0, LeadTimes.MaxDays).WithName("Delivery days");
    }
}

/// <summary>
/// Validates <see cref="DeliveryAreaListRequest"/>.
/// </summary>
public class DeliveryAreaListRequestValidator : AbstractValidator<DeliveryAreaListRequest>
{
    /// <summary>Creates the rules.</summary>
    public DeliveryAreaListRequestValidator() => Include(new PagedRequestValidator());
}
