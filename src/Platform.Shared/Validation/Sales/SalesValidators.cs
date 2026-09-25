using FluentValidation;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Identity;
using Platform.Shared.Dtos.Sales;
using Platform.Shared.Validation.Common;

namespace Platform.Shared.Validation.Sales;

/// <summary>
/// Validates <see cref="UpdateCustomerRequest"/>.
/// </summary>
public class UpdateCustomerRequestValidator : AbstractValidator<UpdateCustomerRequest>
{
    /// <summary>Creates the rules.</summary>
    public UpdateCustomerRequestValidator()
    {
        RuleFor(x => x.Name).ValidName();
        RuleFor(x => x.Phone).ValidOptionalPhone();
        RuleFor(x => x.CompanyName).ValidOptionalName().WithName("Company");
        RuleFor(x => x.Gstin).ValidOptionalGstin().WithName("GSTIN");
    }
}

/// <summary>
/// Validates <see cref="OrderListRequest"/>.
/// </summary>
public class OrderListRequestValidator : AbstractValidator<OrderListRequest>
{
    /// <summary>Creates the rules.</summary>
    public OrderListRequestValidator()
    {
        Include(new PagedRequestValidator());
        RuleFor(x => x.Status).IsInEnum();
    }
}

/// <summary>
/// Validates <see cref="CancelOrderRequest"/>.
/// </summary>
public class CancelOrderRequestValidator : AbstractValidator<CancelOrderRequest>
{
    /// <summary>Creates the rules.</summary>
    public CancelOrderRequestValidator()
    {
        RuleFor(x => x.Remarks).ValidOptionalRemarks().WithName("Reason");
    }
}

/// <summary>
/// Validates <see cref="UpdateBusinessSettingsRequest"/>: the GSTIN must belong to the registered state.
/// </summary>
public class UpdateBusinessSettingsRequestValidator : AbstractValidator<UpdateBusinessSettingsRequest>
{
    /// <summary>Creates the rules.</summary>
    public UpdateBusinessSettingsRequestValidator()
    {
        RuleFor(x => x.LegalName).ValidName().WithName("Legal name");
        RuleFor(x => x.Gstin).ValidGstin().WithName("GSTIN");
        RuleFor(x => x.Address).NotNull().SetValidator(new AddressDtoValidator());
        RuleFor(x => x.Address.State).ValidIndianState().WithName("State");
        RuleFor(x => x.Gstin)
            .Must((request, gstin) => IndianStates.Find(request.Address.State) is not { } state
                || gstin.Length < 2
                || gstin[..2] == state.GstCode)
            .WithMessage(request => $"A GSTIN registered in {request.Address.State} starts with {IndianStates.Find(request.Address.State)?.GstCode}.");
    }
}

