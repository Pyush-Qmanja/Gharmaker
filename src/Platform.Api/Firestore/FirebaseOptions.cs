using System.ComponentModel.DataAnnotations;

namespace Platform.Api.Firestore;

/// <summary>
/// Firebase settings for both Firestore and Authentication, bound from the
/// <c>Firebase</c> section and validated at startup.
/// </summary>
public sealed class FirebaseOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Firebase";

    /// <summary>Firebase project id, e.g. <c>menutesting-82152</c>.</summary>
    [Required]
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Absolute path to the service-account JSON key. Kept outside the repo and
    /// set only in git-ignored config. Not needed when both emulators are used.
    /// </summary>
    public string? CredentialsPath { get; set; }

    /// <summary>
    /// Web API key (Project settings → General). Used only to ask Firebase
    /// Authentication to verify an email and password. Kept out of the repo.
    /// </summary>
    public string? WebApiKey { get; set; }

    /// <summary><c>host:port</c> of the Firestore emulator; when set, Firestore calls go there.</summary>
    public string? FirestoreEmulatorHost { get; set; }

    /// <summary><c>host:port</c> of the Authentication emulator; when set, sign-in calls go there.</summary>
    public string? AuthEmulatorHost { get; set; }

    /// <summary>True when Firestore runs against the local emulator.</summary>
    public bool UseFirestoreEmulator => !string.IsNullOrWhiteSpace(FirestoreEmulatorHost);

    /// <summary>True when Authentication runs against the local emulator.</summary>
    public bool UseAuthEmulator => !string.IsNullOrWhiteSpace(AuthEmulatorHost);
}
