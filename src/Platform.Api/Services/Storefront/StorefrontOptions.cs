using System.ComponentModel.DataAnnotations;

namespace Platform.Api.Services.Storefront;

/// <summary>
/// Settings of the online store (<c>Storefront</c> section).
/// </summary>
public sealed class StorefrontOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Storefront";

    /// <summary>
    /// Organisation the store sells for. Empty means the only active
    /// organisation (the platform is single-tenant until decision 6 is closed).
    /// </summary>
    public Guid? OrganisationId { get; set; }

    /// <summary>
    /// Minutes an order's stock stays held before, unconfirmed, it is given
    /// back (P4). Default two days; confirmation arrives in Phase 4.
    /// </summary>
    [Range(1, 43200)]
    public int HoldMinutes { get; set; } = 2880;

    /// <summary>Seconds between sweeps for expired holds.</summary>
    [Range(5, 3600)]
    public int ExpirySweepSeconds { get; set; } = 60;
}
