using System.ComponentModel.DataAnnotations;

namespace Platform.Web.Options;

/// <summary>
/// Where the UI finds the Web API. Bound from the <c>Api</c> configuration section.
/// </summary>
public sealed class ApiOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Api";

    /// <summary>Base URL of Platform.Api, ending with a slash.</summary>
    [Required, Url]
    public string BaseUrl { get; set; } = string.Empty;
}
