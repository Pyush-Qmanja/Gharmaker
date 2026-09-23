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

    /// <summary>E.164 phone number, e.g. <c>+919876543210</c>.</summary>
    private const string PhonePattern = @"^\+[1-9]\d{7,14}$";

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
    /// Optional phone number in E.164 format.
    /// </summary>
    /// <typeparam name="T">Object being validated.</typeparam>
    /// <param name="rule">Rule builder for the property.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, string?> ValidOptionalPhone<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Matches(PhonePattern)
            .WithMessage("'{PropertyName}' must be in international format, e.g. +919876543210.");

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
    /// True when the value is empty (the field is optional) or an absolute http(s) URL.
    /// </summary>
    /// <param name="value">Candidate URL.</param>
    /// <returns>Whether the value is acceptable.</returns>
    private static bool BeAbsoluteHttpUrl(string? value) =>
        string.IsNullOrEmpty(value)
        || (Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));
}
