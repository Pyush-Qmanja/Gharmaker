using FluentValidation;
using Platform.Shared.Dtos.Catalog;
using Platform.Shared.Validation.Common;

namespace Platform.Shared.Validation.Catalog;

/// <summary>
/// Rules for the unit fields common to create and update.
/// </summary>
internal sealed class UomFieldsValidator : AbstractValidator<IUomFields>
{
    /// <summary>
    /// Defines the shared unit rules.
    /// </summary>
    public UomFieldsValidator()
    {
        RuleFor(x => x.Code).ValidCode();
        RuleFor(x => x.Name).ValidName();
        RuleFor(x => x.Dimension).IsInEnum();
        RuleFor(x => x.BaseFactor).GreaterThan(0).When(x => x.BaseFactor.HasValue);
    }
}

/// <summary>
/// Validates <see cref="CreateUomRequest"/>.
/// </summary>
public class CreateUomRequestValidator : AbstractValidator<CreateUomRequest>
{
    /// <summary>
    /// Applies the shared unit rules.
    /// </summary>
    public CreateUomRequestValidator() => Include(new UomFieldsValidator());
}

/// <summary>
/// Validates <see cref="UpdateUomRequest"/>.
/// </summary>
public class UpdateUomRequestValidator : AbstractValidator<UpdateUomRequest>
{
    /// <summary>
    /// Applies the shared unit rules.
    /// </summary>
    public UpdateUomRequestValidator() => Include(new UomFieldsValidator());
}

/// <summary>
/// Validates <see cref="CatalogBrowseRequest"/>: the usual paging rules.
/// </summary>
public class CatalogBrowseRequestValidator : AbstractValidator<CatalogBrowseRequest>
{
    /// <summary>
    /// Applies the paging rules.
    /// </summary>
    public CatalogBrowseRequestValidator() => Include(new PagedRequestValidator());
}

/// <summary>
/// Validates <see cref="SkuConversionRequest"/>.
/// </summary>
public class SkuConversionRequestValidator : AbstractValidator<SkuConversionRequest>
{
    /// <summary>
    /// Defines the conversion request rules.
    /// </summary>
    public SkuConversionRequestValidator()
    {
        RuleFor(x => x.Value).GreaterThanOrEqualTo(0);
        RuleFor(x => x.From).NotEmpty();
        RuleFor(x => x.To).NotEmpty();
    }
}
