using System.Text.Json;
using System.Text.Json.Serialization;

namespace Platform.Shared.Common;

/// <summary>
/// JSON settings shared by the API (what it writes and accepts) and the UI's
/// API client (what it sends and reads), so both sides always agree.
/// </summary>
/// <remarks>
/// camelCase property names; enums as their names (<c>"Global"</c>, not <c>0</c>),
/// which is also how Firestore stores them. Numeric enum values are still accepted.
/// </remarks>
public static class JsonDefaults
{
    /// <summary>Ready-made options for clients that serialise on their own.</summary>
    public static readonly JsonSerializerOptions Options = Apply(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    /// <summary>
    /// Applies the platform settings to an existing options object (e.g. ASP.NET Core's).
    /// </summary>
    /// <param name="options">Options to configure.</param>
    /// <returns>The same options, for chaining.</returns>
    public static JsonSerializerOptions Apply(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
