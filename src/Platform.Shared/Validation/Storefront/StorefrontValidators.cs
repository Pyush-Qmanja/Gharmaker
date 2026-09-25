using FluentValidation;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Storefront;
using Platform.Shared.Validation.Common;

namespace Platform.Shared.Validation.Storefront;

/// <summary>
/// Validates <see cref="ShopAddressDto"/>: a deliverable Indian address.
/// </summary>
public class ShopAddressDtoValidator : AbstractValidator<ShopAddressDto>
{
    /// <summary>Creates the rules.</summary>
    public ShopAddressDtoValidator()
    {
        RuleFor(x => x.Line1).NotEmpty().MaximumLength(FieldLengths.AddressLine).WithName("Address");
        RuleFor(x => x.Line2).MaximumLength(FieldLengths.AddressLine).WithName("Area / landmark");
        RuleFor(x => x.City).NotEmpty().MaximumLength(FieldLengths.Place);
        RuleFor(x => x.State).ValidIndianState();
        RuleFor(x => x.Pincode).ValidPincode().WithName("PIN code");
    }
}

/// <summary>
/// Validates <see cref="ShopRegisterRequest"/>.
/// </summary>
public class ShopRegisterRequestValidator : AbstractValidator<ShopRegisterRequest>
{
    /// <summary>Creates the rules.</summary>
    public ShopRegisterRequestValidator()
    {
        RuleFor(x => x.Name).ValidName();
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.Password).ValidPassword();
        RuleFor(x => x.Phone).ValidOptionalPhone();
        RuleFor(x => x.CompanyName).ValidOptionalName().WithName("Company");
    }
}

/// <summary>
/// Validates <see cref="UpdateShopProfileRequest"/>.
/// </summary>
public class UpdateShopProfileRequestValidator : AbstractValidator<UpdateShopProfileRequest>
{
    /// <summary>Creates the rules.</summary>
    public UpdateShopProfileRequestValidator()
    {
        RuleFor(x => x.Name).ValidName();
        RuleFor(x => x.Phone).ValidOptionalPhone();
        RuleFor(x => x.CompanyName).ValidOptionalName().WithName("Company");
        RuleFor(x => x.Gstin).ValidOptionalGstin().WithName("GSTIN");
        RuleFor(x => x.Address!).SetValidator(new ShopAddressDtoValidator()).When(x => x.Address is not null);
    }
}

/// <summary>
/// Validates <see cref="ShopProductRequest"/>.
/// </summary>
public class ShopProductRequestValidator : AbstractValidator<ShopProductRequest>
{
    /// <summary>Creates the rules.</summary>
    public ShopProductRequestValidator()
    {
        Include(new PagedRequestValidator());
        RuleFor(x => x.Pincode!).ValidPincode().When(x => !string.IsNullOrEmpty(x.Pincode)).WithName("PIN code");
    }
}

/// <summary>
/// Validates <see cref="AddCartLineRequest"/>.
/// </summary>
public class AddCartLineRequestValidator : AbstractValidator<AddCartLineRequest>
{
    /// <summary>Creates the rules.</summary>
    public AddCartLineRequestValidator()
    {
        RuleFor(x => x.SkuId).NotEmpty().WithMessage("Choose a variant.");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Enter a quantity greater than zero.").ValidQuantity();
        RuleFor(x => x.Uom).NotEmpty().MaximumLength(FieldLengths.Code).WithName("Unit");
    }
}

/// <summary>
/// Validates <see cref="UpdateCartLineRequest"/>.
/// </summary>
public class UpdateCartLineRequestValidator : AbstractValidator<UpdateCartLineRequest>
{
    /// <summary>Creates the rules.</summary>
    public UpdateCartLineRequestValidator()
    {
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Enter a quantity greater than zero.").ValidQuantity();
        RuleFor(x => x.Uom).NotEmpty().MaximumLength(FieldLengths.Code).WithName("Unit");
    }
}

/// <summary>
/// Validates <see cref="SetPincodeRequest"/>.
/// </summary>
public class SetPincodeRequestValidator : AbstractValidator<SetPincodeRequest>
{
    /// <summary>Creates the rules.</summary>
    public SetPincodeRequestValidator()
    {
        RuleFor(x => x.Pincode).ValidPincode().WithName("PIN code");
    }
}

/// <summary>
/// Validates <see cref="CheckoutRequest"/>.
/// </summary>
public class CheckoutRequestValidator : AbstractValidator<CheckoutRequest>
{
    /// <summary>Creates the rules.</summary>
    public CheckoutRequestValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty().WithMessage("Reload the checkout page and try again.");
        RuleFor(x => x.Address).NotNull().SetValidator(new ShopAddressDtoValidator());
        RuleFor(x => x.Phone).NotEmpty().WithMessage("Enter a mobile number for the delivery.").ValidOptionalPhone();
        RuleFor(x => x.Gstin).ValidOptionalGstin().WithName("GSTIN");
    }
}
