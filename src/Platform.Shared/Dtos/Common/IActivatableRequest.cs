namespace Platform.Shared.Dtos.Common;

/// <summary>
/// An update request that can deactivate or restore a master record. The UI
/// renders the Active checkbox for any form model implementing this, so no
/// entity form repeats it.
/// </summary>
public interface IActivatableRequest
{
    /// <summary>False to deactivate, true to restore.</summary>
    bool IsActive { get; set; }
}
