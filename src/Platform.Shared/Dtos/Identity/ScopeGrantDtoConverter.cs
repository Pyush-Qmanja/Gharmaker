using System.ComponentModel;
using System.Globalization;
using Platform.Shared.Entities.Identity;

namespace Platform.Shared.Dtos.Identity;

/// <summary>
/// Converts a scope grant to and from its compact text form: <c>Global</c> or
/// <c>Warehouse:&lt;id&gt;</c>. This lets an HTML form post scopes as plain
/// checkbox values (the MVC binder uses this converter), while JSON keeps the
/// structured <c>{ scopeType, scopeId }</c> shape.
/// </summary>
public sealed class ScopeGrantDtoConverter : TypeConverter
{
    /// <summary>Separator between the scope type and id.</summary>
    private const char Separator = ':';

    /// <summary>
    /// Formats a grant as text.
    /// </summary>
    /// <param name="grant">Grant to format.</param>
    /// <returns><c>Global</c> or <c>Type:id</c>.</returns>
    public static string Format(ScopeGrantDto grant) =>
        grant.ScopeId is { } id ? $"{grant.ScopeType}{Separator}{id}" : grant.ScopeType.ToString();

    /// <summary>
    /// Reports that strings can be converted.
    /// </summary>
    /// <param name="context">Unused.</param>
    /// <param name="sourceType">Type being converted from.</param>
    /// <returns>True for <see cref="string"/>.</returns>
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    /// <summary>
    /// Parses <c>Global</c> or <c>Type:id</c> into a grant.
    /// </summary>
    /// <param name="context">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <param name="value">Text to parse.</param>
    /// <returns>The grant.</returns>
    /// <exception cref="FormatException">The text is not a valid grant.</exception>
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not string text)
        {
            return base.ConvertFrom(context, culture, value);
        }

        string[] parts = text.Split(Separator, 2);
        if (!Enum.TryParse(parts[0], ignoreCase: false, out ScopeType type) || !Enum.IsDefined(type))
        {
            throw new FormatException($"'{text}' is not a valid scope.");
        }

        Guid? id = null;
        if (parts.Length == 2)
        {
            id = Guid.TryParse(parts[1], out Guid parsed) ? parsed : throw new FormatException($"'{text}' is not a valid scope.");
        }

        return new ScopeGrantDto { ScopeType = type, ScopeId = id };
    }
}
