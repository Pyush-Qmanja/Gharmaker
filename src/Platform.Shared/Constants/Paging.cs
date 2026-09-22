namespace Platform.Shared.Constants;

/// <summary>
/// Paging limits applied to every list endpoint and every list screen.
/// </summary>
public static class Paging
{
    /// <summary>Page size used when the caller does not specify one.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>Largest page size a caller may request.</summary>
    public const int MaxPageSize = 100;
}
