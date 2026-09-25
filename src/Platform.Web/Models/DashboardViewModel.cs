using Platform.Web.Common;

namespace Platform.Web.Models;

/// <summary>
/// The dashboard: a greeting, one card per module the user can open (with its
/// record count), and shortcuts to the actions they are allowed to take.
/// </summary>
public sealed class DashboardViewModel
{
    /// <summary>"Good morning", "Good afternoon" or "Good evening" (IST).</summary>
    public string Greeting { get; init; } = string.Empty;

    /// <summary>Today's date in IST, for the subtitle.</summary>
    public string Today { get; init; } = string.Empty;

    /// <summary>Modules the user can open, in menu order.</summary>
    public IReadOnlyList<DashboardModule> Modules { get; init; } = Array.Empty<DashboardModule>();

    /// <summary>Actions the user may take.</summary>
    public IReadOnlyList<QuickAction> Actions { get; init; } = Array.Empty<QuickAction>();
}

/// <summary>
/// One module card.
/// </summary>
/// <param name="Item">The screen.</param>
/// <param name="Count">How many records it holds (what the user can see), or null if unknown.</param>
public sealed record DashboardModule(NavigationItem Item, int? Count);

/// <summary>
/// One shortcut on the dashboard.
/// </summary>
/// <param name="Title">Button text.</param>
/// <param name="Description">One line under it.</param>
/// <param name="Icon">Icon name from <see cref="Icons"/>.</param>
/// <param name="Controller">MVC controller.</param>
/// <param name="Action">MVC action.</param>
/// <param name="Capability">Capability needed to see it.</param>
/// <param name="Area">MVC area, e.g. Shop for the online store; null for the admin app.</param>
public sealed record QuickAction(string Title, string Description, string Icon, string Controller, string Action, string Capability, string? Area = null);
