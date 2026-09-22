using Microsoft.AspNetCore.Authorization;

namespace Platform.Api.Security.Authorization;

/// <summary>
/// Requires one capability for the whole endpoint.
/// </summary>
/// <param name="Capability">Code from <c>Capabilities</c>.</param>
public sealed record CapabilityRequirement(string Capability) : IAuthorizationRequirement;

/// <summary>
/// Requires the "view" capability for reads (GET/HEAD) and the "manage"
/// capability for everything else. Used by CRUD controllers so one attribute
/// guards all five endpoints.
/// </summary>
/// <param name="ViewCapability">Capability for reads.</param>
/// <param name="ManageCapability">Capability for writes.</param>
public sealed record CrudCapabilityRequirement(string ViewCapability, string ManageCapability) : IAuthorizationRequirement;

/// <summary>
/// Guards an endpoint or controller with one capability (P6). Use instead of
/// role checks: <c>[RequiresCapability(Capabilities.UsersManage)]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequiresCapabilityAttribute : AuthorizeAttribute, IAuthorizationRequirementData
{
    /// <summary>
    /// Creates the attribute.
    /// </summary>
    /// <param name="capability">Code from <c>Capabilities</c>.</param>
    public RequiresCapabilityAttribute(string capability)
    {
        Capability = capability;
    }

    /// <summary>Capability required.</summary>
    public string Capability { get; }

    /// <summary>
    /// Supplies the requirement to the authorization system.
    /// </summary>
    /// <returns>The single capability requirement.</returns>
    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        yield return new CapabilityRequirement(Capability);
    }
}

/// <summary>
/// Guards a whole CRUD controller: reads need <c>view</c>, writes need
/// <c>manage</c>. Every controller deriving from <c>CrudControllerBase</c> must carry it.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CrudCapabilitiesAttribute : AuthorizeAttribute, IAuthorizationRequirementData
{
    /// <summary>
    /// Creates the attribute.
    /// </summary>
    /// <param name="viewCapability">Capability for GET endpoints.</param>
    /// <param name="manageCapability">Capability for POST/PUT/DELETE endpoints.</param>
    public CrudCapabilitiesAttribute(string viewCapability, string manageCapability)
    {
        ViewCapability = viewCapability;
        ManageCapability = manageCapability;
    }

    /// <summary>Capability for reads.</summary>
    public string ViewCapability { get; }

    /// <summary>Capability for writes.</summary>
    public string ManageCapability { get; }

    /// <summary>
    /// Supplies the requirement to the authorization system.
    /// </summary>
    /// <returns>The view/manage requirement.</returns>
    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        yield return new CrudCapabilityRequirement(ViewCapability, ManageCapability);
    }
}

/// <summary>
/// Evaluates both capability requirements against <see cref="IPermissionService"/>.
/// A caller lacking the capability gets 403; an inactive or unknown user holds no capabilities.
/// </summary>
public sealed class CapabilityAuthorizationHandler : IAuthorizationHandler
{
    private readonly IPermissionService _permissions;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="permissions">Current caller's capabilities.</param>
    public CapabilityAuthorizationHandler(IPermissionService permissions)
    {
        _permissions = permissions;
    }

    /// <summary>
    /// Marks each capability requirement as met when the caller holds the capability it needs.
    /// </summary>
    /// <param name="context">Authorization context for the request.</param>
    /// <returns>A task that completes once all requirements are evaluated.</returns>
    public async Task HandleAsync(AuthorizationHandlerContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        foreach (IAuthorizationRequirement requirement in context.PendingRequirements.ToList())
        {
            string? needed = requirement switch
            {
                CapabilityRequirement single => single.Capability,
                CrudCapabilityRequirement crud => IsRead(context.Resource) ? crud.ViewCapability : crud.ManageCapability,
                _ => null,
            };

            if (needed is not null && await _permissions.HasCapabilityAsync(needed))
            {
                context.Succeed(requirement);
            }
        }
    }

    /// <summary>
    /// Decides whether the request only reads.
    /// </summary>
    /// <param name="resource">The authorization resource; the current <see cref="HttpContext"/> for endpoints.</param>
    /// <returns>True for GET and HEAD.</returns>
    private static bool IsRead(object? resource) =>
        resource is HttpContext http && (HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method));
}
