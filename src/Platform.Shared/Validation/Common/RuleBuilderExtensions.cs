using FluentValidation;
using Platform.Shared.Constants;

namespace Platform.Shared.Validation.Common;

/// <summary>
/// Reusable validation rules. Every validator composes these instead of
/// repeating lengths, patterns and messages, so a rule changes in one place.
/// </summary>
public static class RuleBuilderExtensions
{
    /// <summary>Lower-case letters, digits and single hyphens, e.g. <c>ultra-tech</c>.</summary>
    private const string SlugPattern = "^[a-z0-9]+(?:-[a-z0-9]+)*$";

    /// <summary>
    /// Letters, digits and single hyphens, e.g. <c>WH-PUNE-01</c>. Either case is
    /// accepted; mappers store codes upper-case.
    /// </summary>
    private const string CodePattern = "^[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*$";

    /// <summary>Six-digit Indian PIN code not starting with 0.</summary>
    private const string PincodePattern = @"^[1-9]\d{5}$";

    /// <summary>HSN code: 4, 6 or 8 digits.</summary>
    private const string HsnPattern = @"^(\d{4}|\d{6}|\d{8})$";

    /// <summary>GSTIN: state code, PAN, entity number, Z, check character.</summary>
    private const string GstinPattern = "^[0-3][0-9][A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$";

    /// <summary>India's calling code.</summary>
    private const string IndiaCode = "+91";

    /// <summary>An Indian mobile: +91 and ten digits starting 6-9.</summary>
    private static readonly System.Text.RegularExpressions.Regex IndianMobile = new(@"^\+91[6-9]\d{9}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>E.164 phone number, e.g. <c>+971501234567</c>.</summary>
    private static readonly System.Text.RegularExpressions.Regex Phone = new(@"^\+[1-9]\d{7,14}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Required display name within <see cref="FieldLengths.Name"/>.
    /// </summary>
    /// <typeparam name="T">Object being validated.</typeparam>
    /// <param name="rule">Rule builder for the property.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, string> ValidName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(FieldLengths.Name);

    /// <summary>
    /// Optional free-text name within <see cref="FieldLengths.Name"/>.
    /// </summary>
    /// <typeparam name="T">Object being validated.</typeparam>
    /// <param name="rule">Rule builder for the property.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, string?> ValidOptionalName<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(FieldLengths.Name);

    /// <summary>
    /// Required URL-safe slug: lower-case letters, digits and hyphens.
    /// </summary>
    /// <typeparam name="T">Object being validated.</typeparam>
    /// <param name="rule">Rule builder for the property.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, string> ValidSlug<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MaximumLength(FieldLengths.Slug)
            .Matches(SlugPattern)
            .WithMessage("'{PropertyName}' may contain only lower-case letters, digits and single hyphens.");

    /// <summary>
    /// Optional absolute http(s) URL within <see cref="FieldLengths.Url"/>.
    /// </summary>
    /// <typeparam name="T">Object being validated.</typeparam>
    /// <param name="rule">Rule builder for the property.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, string?> ValidOptionalUrl<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(FieldLengths.Url)
            .Must(BeAbsoluteHttpUrl)
            .WithMessage("'{PropertyName}' must be an absolute http or https URL.");

    /// <summary>
    /// Required email address within <see cref="FieldLengths.Email"/>.
    /// </summary>
    /// <typeparam name="T">Object being validated.</typeparam>
    /// <param name="rule">Rule builder for the property.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(FieldLengths.Email).EmailAddress();

    /// <summary>
    /// Optional phone number in E.164 format. An Indian (+91) number must be a
    /// 10-digit mobile and gets only that message, not the general one as well.
    /// </summary>
    /// <typeparam name="T">Object being validated.</typeparam>
    /// <param name="rule">Rule builder for the property.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, string?> ValidOptionalPhone<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(p => p is null || !IsIndian(p) || IndianMobile.IsMatch(p))
            .WithMessage("Enter a 10-digit Indian mobile number starting with 6, 7, 8 or 9, e.g. 98765 43210.")
            .Must(p => p is null || IsIndian(p) || Phone.IsMatch(p))
            .WithMessage("Enter a valid mobile number with its country code, e.g. +91 98765 43210.");

    /// <summary>
    /// Whether a phone number carries India's calling code.
    /// </summary>
    /// <param name="phone">Phone number.</param>
    /// <returns>True when it starts with +91.</returns>
    private static bool IsIndian(string phone) => phone.StartsWith(IndiaCode, StringComparison.Ordinal);

    /// <summary>
    /// Required password between <see cref="FieldLengths.PasswordMin"/> and
    /// <see cref="FieldLengths.PasswordMax"/> characters.
    /// </summary>
    /// <typeparam name="T">Object being validated.</typeparam>
    /// <param name="rule">Rule builder for the property.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Length(FieldLengths.PasswordMin, FieldLengths.PasswordMax);

    /// <summary>
    /// Required business code: letters, digits and hyphens, within <see cref="FieldLengths.Code"/>.
    /// </summary>
    /// <typeparam name="T">Object being validated.</typeparam>
    /// <param name="rule">Rule builder for the property.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, string> ValidCode<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MaximumLength(FieldLengths.Code)
            .Matches(CodePattern)
            .WithMessage("'{PropertyName}' may contain only letters, digits and single hyphens, e.g. WH-PUNE-01.");

    /// <summary>
    /// Required six-digit Indian PIN code.
    /// </summary>
    /// <typeparam name="T">Object being validated.</typeparam>
    /// <param name="rule">Rule builder for the property.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, string> ValidPincode<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Matches(PincodePattern).WithMessage("'{PropertyName}' must be a six-digit PIN code.");

    /// <summary>
    /// Optional latitude between -90 and 90.
    /// </summary>
    /// <typeparam name="T">Object being validated.</typeparam>
    /// <param name="rule">Rule builder for the property.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, double?> ValidOptionalLat<T>(this IRuleBuilder<T, double?> rule) =>
        rule.InclusiveBetween(-90, 90);

    /// <summary>
    /// Optional longitude between -180 and 180.
    /// </summary>
    /// <typeparam name="T">Object being validated.</typeparam>
    /// <param name="rule">Rule builder for the property.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, double?> ValidOptionalLng<T>(this IRuleBuilder<T, double?> rule) =>
        rule.InclusiveBetween(-180, 180);

    /// <summary>
    /// Sort position that is zero or greater.
    /// </summary>
    /// <typeparam name="T">Object being validated.</typeparam>
    /// <param name="rule">Rule builder for the property.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, int> ValidDisplayOrder<T>(this IRuleBuilder<T, int> rule) =>
        rule.GreaterThanOrEqualTo(0);

    /// <summary>
    /// Optional free-text notes within <see cref="FieldLengths.Remarks"/>.
    /// </summary>
    /// <typeparam name="T">Object being validated.</typeparam>
    /// <param name="rule">Rule builder for the property.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, string?> ValidOptionalRemarks<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(FieldLengths.Remarks);

    /// <summary>
    /// Required free-text notes (e.g. the reason for an adjustment) within <see cref="FieldLengths.Remarks"/>.
    /// </summary>
    /// <typeparam name="T">Object being validated.</typeparam>
    /// <param name="rule">Rule builder for the property.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, string?> ValidRequiredRemarks<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().WithMessage("Say why, in a few words.").MaximumLength(FieldLengths.Remarks);

    /// <summary>
    /// Optional human reference (e.g. a supplier's invoice number) within <see cref="FieldLengths.ReferenceNo"/>.
    /// </summary>
    /// <typeparam name="T">Object being validated.</typeparam>
    /// <param name="rule">Rule builder for the property.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, string?> ValidOptionalReferenceNo<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(FieldLengths.ReferenceNo);

    /// <summary>
    /// A quantity with at most four decimal places (<c>Quantity.Scale</c>), within a sane range.
    /// Sign rules are added by the caller.
    /// </summary>
    /// <typeparam name="T">Object being validated.</typeparam>
    /// <param name="rule">Rule builder for the property.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, decimal> ValidQuantity<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.InclusiveBetween(-1_000_000_000m, 1_000_000_000m)
            .PrecisionScale(18, 4, ignoreTrailingZeros: true)
            .WithMessage("'{PropertyName}' can have at most 4 decimal places.");

    /// <summary>
    /// An HSN code: 4, 6 or 8 digits.
    /// </summary>
    /// <typeparam name="T">Object being validated.</typeparam>
    /// <param name="rule">Rule for the property.</param>
    /// <returns>The rule, for chaining.</returns>
    public static IRuleBuilderOptions<T, string> ValidHsnCode<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .Matches(HsnPattern)
            .WithMessage("'{PropertyName}' must be 4, 6 or 8 digits.");

    /// <summary>
    /// An optional GSTIN: 15 characters, state code first, e.g. 27AAPFU0939F1ZV.
    /// </summary>
    /// <typeparam name="T">Object being validated.</typeparam>
    /// <param name="rule">Rule for the property.</param>
    /// <returns>The rule, for chaining.</returns>
    public static IRuleBuilderOptions<T, string?> ValidOptionalGstin<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Matches(GstinPattern)
            .WithMessage("'{PropertyName}' must be a 15-character GSTIN, e.g. 27AAPFU0939F1ZV.");

    /// <summary>
    /// A required GSTIN.
    /// </summary>
    /// <typeparam name="T">Object being validated.</typeparam>
    /// <param name="rule">Rule for the property.</param>
    /// <returns>The rule, for chaining.</returns>
    public static IRuleBuilderOptions<T, string> ValidGstin<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .Matches(GstinPattern)
            .WithMessage("'{PropertyName}' must be a 15-character GSTIN, e.g. 27AAPFU0939F1ZV.");

    /// <summary>
    /// A unit price: greater than zero, at most four decimals (P8 keeps it exactly as entered).
    /// </summary>
    /// <typeparam name="T">Object being validated.</typeparam>
    /// <param name="rule">Rule for the property.</param>
    /// <returns>The rule, for chaining.</returns>
    public static IRuleBuilderOptions<T, decimal> ValidUnitPrice<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.GreaterThan(0m)
            .LessThanOrEqualTo(100_000_000m)
            .PrecisionScale(18, 4, ignoreTrailingZeros: true)
            .WithMessage("'{PropertyName}' must be more than 0 with at most 4 decimal places.");

    /// <summary>
    /// A percentage between 0 and a limit, with at most three decimals.
    /// </summary>
    /// <typeparam name="T">Object being validated.</typeparam>
    /// <param name="rule">Rule for the property.</param>
    /// <param name="max">Largest allowed value.</param>
    /// <returns>The rule, for chaining.</returns>
    public static IRuleBuilderOptions<T, decimal> ValidPercent<T>(this IRuleBuilder<T, decimal> rule, decimal max) =>
        rule.InclusiveBetween(0m, max)
            .PrecisionScale(7, 3, ignoreTrailingZeros: true)
            .WithMessage($"'{{PropertyName}}' must be between 0 and {max} with at most 3 decimal places.");

    /// <summary>
    /// A state name that is one of India's states or union territories (GST needs to recognise it).
    /// </summary>
    /// <typeparam name="T">Object being validated.</typeparam>
    /// <param name="rule">Rule for the property.</param>
    /// <returns>The rule, for chaining.</returns>
    public static IRuleBuilderOptions<T, string> ValidIndianState<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .Must(IndianStates.IsKnown)
            .WithMessage("'{PropertyName}' must be an Indian state or union territory, e.g. Maharashtra.");

    /// <summary>
    /// True when the value is empty (the field is optional) or an absolute http(s) URL.
    /// </summary>
    /// <param name="value">Candidate URL.</param>
    /// <returns>Whether the value is acceptable.</returns>
    private static bool BeAbsoluteHttpUrl(string? value) =>
        string.IsNullOrEmpty(value)
        || (Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));
}
