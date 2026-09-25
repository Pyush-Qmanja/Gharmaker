using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Platform.Api.Common;
using Platform.Api.Common.Exceptions;
using Platform.Api.Firestore;
using Platform.Api.Repositories;
using Platform.Shared.Entities.Identity;
using Platform.Shared.Entities.Sales;

namespace Platform.Api.Services.Storefront;

/// <summary>
/// Knows which organisation the online store sells for, so a visitor who is
/// not signed in still sees that organisation's catalogue.
/// </summary>
public interface IStorefrontOrganisation
{
    /// <summary>
    /// Returns the store's organisation id.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The organisation id.</returns>
    Task<Guid> GetIdAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="IStorefrontOrganisation"/> from <c>Storefront:OrganisationId</c>,
/// or else the only active organisation (cached for ten minutes).
/// </summary>
public sealed class StorefrontOrganisation : IStorefrontOrganisation
{
    /// <summary>Cache key of the resolved id.</summary>
    private const string CacheKey = "storefront:organisation";

    /// <summary>How long the resolved id is kept.</summary>
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);

    private readonly IFirestoreContext _context;
    private readonly IMemoryCache _cache;
    private readonly StorefrontOptions _options;

    /// <summary>
    /// Creates the resolver.
    /// </summary>
    /// <param name="context">Firestore.</param>
    /// <param name="cache">Keeps the answer.</param>
    /// <param name="options">Store settings.</param>
    public StorefrontOrganisation(IFirestoreContext context, IMemoryCache cache, IOptions<StorefrontOptions> options)
    {
        _context = context;
        _cache = cache;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<Guid> GetIdAsync(CancellationToken cancellationToken = default)
    {
        if (_options.OrganisationId is { } configured)
        {
            return configured;
        }

        if (_cache.TryGetValue(CacheKey, out Guid cached))
        {
            return cached;
        }

        QuerySnapshot organisations = await _context.Collection<Organisation>().Limit(10).GetSnapshotAsync(cancellationToken);
        Organisation organisation = organisations.Documents
            .Select(DocumentConverter.FromDocument<Organisation>)
            .Where(o => o.IsActive)
            .OrderBy(o => o.CreatedAt)
            .FirstOrDefault()
            ?? throw new NotFoundException("Store");
        return _cache.Set(CacheKey, organisation.Id, CacheFor);
    }
}

/// <summary>
/// Runs every storefront endpoint as the store's organisation. A signed-in
/// customer already carries it in their token (and must belong to it); anyone
/// else — a visitor, or a staff token — browses as an anonymous shopper of the
/// store, so tenancy filtering applies exactly as for a customer.
/// </summary>
public sealed class StorefrontIdentityFilter : IAsyncActionFilter
{
    private readonly IStorefrontOrganisation _organisation;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// Creates the filter.
    /// </summary>
    /// <param name="organisation">The store's organisation.</param>
    /// <param name="currentUser">Caller.</param>
    public StorefrontIdentityFilter(IStorefrontOrganisation organisation, ICurrentUser currentUser)
    {
        _organisation = organisation;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Sets the identity, then runs the action.
    /// </summary>
    /// <param name="context">Action context.</param>
    /// <param name="next">The action.</param>
    /// <returns>A task that completes with the action.</returns>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        Guid orgId = await _organisation.GetIdAsync(context.HttpContext.RequestAborted);
        if (_currentUser.IsCustomer)
        {
            if (_currentUser.OrgId != orgId)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            await next();
            return;
        }

        using (SystemIdentity.Use(orgId, userId: null))
        {
            await next();
        }
    }
}

/// <summary>
/// The signed-in customer of the current storefront request.
/// </summary>
public interface ICustomerContext
{
    /// <summary>
    /// Returns the signed-in, active customer, or null for a visitor.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The customer, or null.</returns>
    Task<Customer?> FindAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the signed-in, active customer, or fails with 401.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The customer.</returns>
    Task<Customer> RequireAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="ICustomerContext"/> read from the token and the database once per request.
/// A blocked customer counts as signed out at once, whatever their token says.
/// </summary>
public sealed class CustomerContext : ICustomerContext
{
    private readonly ICurrentUser _currentUser;
    private readonly IRepository<Customer> _customers;
    private Customer? _customer;
    private bool _loaded;

    /// <summary>
    /// Creates the context.
    /// </summary>
    /// <param name="currentUser">Caller.</param>
    /// <param name="customers">Customer data access.</param>
    public CustomerContext(ICurrentUser currentUser, IRepository<Customer> customers)
    {
        _currentUser = currentUser;
        _customers = customers;
    }

    /// <inheritdoc />
    public async Task<Customer?> FindAsync(CancellationToken cancellationToken = default)
    {
        if (!_loaded)
        {
            _loaded = true;
            _customer = _currentUser.IsCustomer && _currentUser.UserId is { } id
                ? await _customers.GetByIdAsync(id, cancellationToken)
                : null;
            if (_customer is { IsActive: false })
            {
                _customer = null;
            }
        }

        return _customer;
    }

    /// <inheritdoc />
    public async Task<Customer> RequireAsync(CancellationToken cancellationToken = default) =>
        await FindAsync(cancellationToken) ?? throw new AuthenticationFailedException();
}
