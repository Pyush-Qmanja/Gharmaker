using Microsoft.AspNetCore.Authorization;
using Platform.Shared.Constants;

namespace Platform.Api.Security.Authorization;

/// <summary>
/// The two kinds of caller and the policies that keep them apart. Every
/// <c>[Authorize]</c> without a named policy (so every staff endpoint, including
/// the capability attributes) uses <see cref="StaffDefault"/>, which refuses a
/// customer token; storefront endpoints name <see cref="Customer"/>.
/// </summary>
public static class AuthPolicies
{
    /// <summary>Name of the policy for signed-in storefront customers.</summary>
    public const string Customer = "customer";

    /// <summary>
    /// Default policy: signed in, and not a customer.
    /// </summary>
    public static readonly AuthorizationPolicy StaffDefault = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireAssertion(context => context.User.FindFirst(ClaimNames.Actor)?.Value != Actors.Customer)
        .Build();

    /// <summary>
    /// Customer policy: signed in with a customer token.
    /// </summary>
    public static readonly AuthorizationPolicy CustomerPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim(ClaimNames.Actor, Actors.Customer)
        .Build();

    /// <summary>
    /// Registers both policies.
    /// </summary>
    /// <param name="options">Authorization options.</param>
    public static void Configure(AuthorizationOptions options)
    {
        options.DefaultPolicy = StaffDefault;
        options.AddPolicy(Customer, CustomerPolicy);
    }
}

/// <summary>
/// Limits an endpoint to signed-in storefront customers.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class CustomerOnlyAttribute : AuthorizeAttribute
{
    /// <summary>
    /// Creates the attribute.
    /// </summary>
    public CustomerOnlyAttribute()
        : base(AuthPolicies.Customer)
    {
    }
}
