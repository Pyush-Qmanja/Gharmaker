using FluentValidation;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Pricing;
using Platform.Shared.Entities.Pricing;
using Platform.Shared.Validation.Common;

namespace Platform.Shared.Validation.Pricing;

/// <summary>
/// Rules shared by creating and updating a price list.
/// </summary>
internal sealed class PriceListFieldsValidator : AbstractValidator<IPriceListFields>
{
    /// <summary>
    /// Creates the rules: a contract list names its customer; other lists name none.
    /// </summary>
    public PriceListFieldsValidator()
    {
        RuleFor(x => x.Name).ValidName();
        RuleFor(x => x.Code).ValidCode();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.CustomerId).NotEmpty()
            .When(x => x.Type == PriceListType.Contract)
            .WithMessage("Choose the customer this contract list is for.");
        RuleFor(x => x.CustomerId).Null()
            .When(x => x.Type != PriceListType.Contract)
            .WithMessage("Only a contract list is for one customer.");
        RuleFor(x => x.Remarks).ValidOptionalRemarks();
    }
}

/// <summary>
/// Validates <see cref="CreatePriceListRequest"/>.
/// </summary>
public class CreatePriceListRequestValidator : AbstractValidator<CreatePriceListRequest>
{
    /// <summary>Creates the rules.</summary>
    public CreatePriceListRequestValidator() => Include(new PriceListFieldsValidator());
}

/// <summary>
/// Validates <see cref="UpdatePriceListRequest"/>.
/// </summary>
public class UpdatePriceListRequestValidator : AbstractValidator<UpdatePriceListRequest>
{
    /// <summary>Creates the rules.</summary>
    public UpdatePriceListRequestValidator() => Include(new PriceListFieldsValidator());
}

/// <summary>
/// Validates <see cref="SetSkuPriceRequest"/>: slabs start at 0, rise strictly, and every rate is positive.
/// </summary>
public class SetSkuPriceRequestValidator : AbstractValidator<SetSkuPriceRequest>
{
    /// <summary>Creates the rules.</summary>
    public SetSkuPriceRequestValidator()
    {
        RuleFor(x => x.PriceListId).NotEmpty().WithMessage("Choose the price list.");
        RuleFor(x => x.SkuId).NotEmpty().WithMessage("Choose the SKU.");
        RuleFor(x => x.Uom).NotEmpty().MaximumLength(FieldLengths.Code).WithName("Unit");
        RuleFor(x => x.Slabs)
            .NotEmpty().WithMessage("Enter a price.")
            .Must(s => s.Count <= FieldLengths.PriceSlabs).WithMessage($"At most {FieldLengths.PriceSlabs} quantity slabs.")
            .Must(s => s.Count == 0 || s[0].MinQuantity == 0).WithMessage("The first slab must start at 0.")
            .Must(s => s.Select(x => x.MinQuantity).Distinct().Count() == s.Count).WithMessage("Two slabs start at the same quantity.");
        RuleForEach(x => x.Slabs).ChildRules(slab =>
        {
            slab.RuleFor(s => s.MinQuantity).GreaterThanOrEqualTo(0).ValidQuantity().WithName("From quantity");
            slab.RuleFor(s => s.UnitPrice).ValidUnitPrice().WithName("Price");
        });
        RuleFor(x => x.Remarks).ValidOptionalRemarks();
    }
}

/// <summary>
/// Validates <see cref="PriceGridRequest"/>.
/// </summary>
public class PriceGridRequestValidator : AbstractValidator<PriceGridRequest>
{
    /// <summary>Creates the rules.</summary>
    public PriceGridRequestValidator()
    {
        Include(new PagedRequestValidator());
        RuleFor(x => x.PriceListId).NotEmpty().WithMessage("Choose the price list.");
    }
}

/// <summary>
/// Validates <see cref="CreateTaxRateRequest"/>.
/// </summary>
public class CreateTaxRateRequestValidator : AbstractValidator<CreateTaxRateRequest>
{
    /// <summary>Most a GST rate can be, in percent.</summary>
    private const decimal MaxRatePercent = 100m;

    /// <summary>Most a cess can be, in percent.</summary>
    private const decimal MaxCessPercent = 400m;

    /// <summary>Creates the rules.</summary>
    public CreateTaxRateRequestValidator()
    {
        RuleFor(x => x.HsnCode).ValidHsnCode().WithName("HSN code");
        RuleFor(x => x.RatePercent).ValidPercent(MaxRatePercent).WithName("GST rate");
        RuleFor(x => x.CessPercent).ValidPercent(MaxCessPercent).WithName("Cess");
        RuleFor(x => x.Remarks).ValidOptionalRemarks();
    }
}

/// <summary>
/// Validates <see cref="TaxRateListRequest"/>.
/// </summary>
public class TaxRateListRequestValidator : AbstractValidator<TaxRateListRequest>
{
    /// <summary>Creates the rules.</summary>
    public TaxRateListRequestValidator()
    {
        Include(new PagedRequestValidator());
        RuleFor(x => x.HsnCode!).ValidHsnCode().When(x => !string.IsNullOrEmpty(x.HsnCode)).WithName("HSN code");
    }
}
