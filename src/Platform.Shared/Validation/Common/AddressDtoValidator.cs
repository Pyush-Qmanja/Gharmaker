using FluentValidation;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Common;

namespace Platform.Shared.Validation.Common;

/// <summary>
/// Validates a postal address wherever one appears (warehouse, site, customer).
/// </summary>
public class AddressDtoValidator : AbstractValidator<AddressDto>
{
    /// <summary>
    /// Defines the address rules.
    /// </summary>
    public AddressDtoValidator()
    {
        RuleFor(x => x.Line1).NotEmpty().MaximumLength(FieldLengths.AddressLine);
        RuleFor(x => x.Line2).MaximumLength(FieldLengths.AddressLine);
        RuleFor(x => x.City).NotEmpty().MaximumLength(FieldLengths.Place);
        RuleFor(x => x.State).NotEmpty().MaximumLength(FieldLengths.Place);
        RuleFor(x => x.Pincode).ValidPincode();
    }
}
