using FluentValidation;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Common;

namespace Platform.Shared.Validation.Common;

/// <summary>
/// Validates paging parameters for every list endpoint.
/// </summary>
public class PagedRequestValidator : AbstractValidator<PagedRequest>
{
    /// <summary>
    /// Defines the paging rules.
    /// </summary>
    public PagedRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
        RuleFor(x => x.Search).MaximumLength(FieldLengths.Name);
    }
}
