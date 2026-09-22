using System.ComponentModel.DataAnnotations;

namespace Platform.Api.Firestore;

/// <summary>
/// Firestore connection settings, bound from the <c>Firestore</c> section and
/// validated at startup.
/// </summary>
public sealed class FirestoreOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Firestore";

    /// <summary>Google Cloud / Firebase project id, e.g. <c>menutesting-82152</c>.</summary>
    [Required]
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Absolute path to the service-account JSON key. Kept outside the repo and
    /// set only in git-ignored config. Leave empty when using the emulator.
    /// </summary>
    public string? CredentialsPath { get; set; }

    /// <summary>
    /// <c>host:port</c> of the local Firestore emulator. When set, the API talks
    /// to the emulator instead of the real project and no credentials are needed.
    /// </summary>
    public string? EmulatorHost { get; set; }
}
