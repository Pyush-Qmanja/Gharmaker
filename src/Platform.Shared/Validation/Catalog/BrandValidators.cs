using FluentValidation;
using Platform.Shared.Dtos.Catalog;
using Platform.Shared.Validation.Common;

namespace Platform.Shared.Validation.Catalog;

/// <summary>
/// Rules for the brand fields common to create and update. Internal so the
/// DI scan registers only the concrete request validators below.
/// </summary>
internal sealed class BrandFieldsValidator : AbstractValidator<IBrandFields>
{
    /// <summary>
    /// Defines the shared brand rules.
    /// </summary>
    public BrandFieldsValidator()
    {
        RuleFor(x => x.Name).ValidName();
        RuleFor(x => x.Slug).ValidSlug();
        RuleFor(x => x.LogoUrl).ValidOptionalUrl();
        RuleFor(x => x.ManufacturerName).ValidOptionalName();
        RuleFor(x => x.DisplayOrder).ValidDisplayOrder();
    }
}

/// <summary>
/// Validates <see cref="CreateBrandRequest"/>.
/// </summary>
public class CreateBrandRequestValidator : AbstractValidator<CreateBrandRequest>
{
    /// <summary>
    /// Applies the shared brand rules.
    /// </summary>
    public CreateBrandRequestValidator() => Include(new BrandFieldsValidator());
}

/// <summary>
/// Validates <see cref="UpdateBrandRequest"/>.
/// </summary>
public class UpdateBrandRequestValidator : AbstractValidator<UpdateBrandRequest>
{
    /// <summary>
    /// Applies the shared brand rules.
    /// </summary>
    public UpdateBrandRequestValidator() => Include(new BrandFieldsValidator());
}
