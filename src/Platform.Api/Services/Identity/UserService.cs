using Google.Cloud.Firestore;
using Platform.Api.Common;
using Platform.Api.Common.Exceptions;
using Platform.Api.Firestore;
using Platform.Api.Mapping;
using Platform.Api.Repositories;
using Platform.Api.Security;
using Platform.Api.Security.Authorization;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Identity;
using Platform.Shared.Entities.Identity;
using Platform.Shared.Entities.Inventory;

namespace Platform.Api.Services.Identity;

/// <summary>
/// CRUD for users. Keeps two records in step: the Firebase Authentication
/// account (credentials, sign-in enabled) and the platform user in Firestore
/// (profile, organisation, roles, scopes).
/// </summary>
public sealed class UserService : CrudService<User, UserDto, CreateUserRequest, UpdateUserRequest>
{
    private readonly IRepository<Role> _roles;
    private readonly IIdentityProvider _identityProvider;
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionService _permissions;
    private readonly IRepository<Warehouse> _warehouses;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="repository">User data access.</param>
    /// <param name="unitOfWork">Commits staged changes.</param>
    /// <param name="mapper">User mapping.</param>
    /// <param name="roles">Role data access, to validate assigned roles.</param>
    /// <param name="identityProvider">Firebase Authentication account management.</param>
    /// <param name="currentUser">Caller, to stop self-deactivation.</param>
    /// <param name="permissions">Caller's scopes, to stop granting access they do not hold.</param>
    /// <param name="warehouses">Warehouse data access, to validate warehouse scopes.</param>
    public UserService(
        IRepository<User> repository,
        IUnitOfWork unitOfWork,
        IEntityMapper<User, UserDto, CreateUserRequest, UpdateUserRequest> mapper,
        IRepository<Role> roles,
        IIdentityProvider identityProvider,
        ICurrentUser currentUser,
        IPermissionService permissions,
        IRepository<Warehouse> warehouses)
        : base(repository, unitOfWork, mapper)
    {
        _roles = roles;
        _identityProvider = identityProvider;
        _currentUser = currentUser;
        _permissions = permissions;
        _warehouses = warehouses;
    }

    /// <summary>
    /// Creates the Firebase account first, then the platform user. If saving
    /// the user fails, the account is deleted again so no orphan can sign in.
    /// </summary>
    /// <param name="request">Validated create request.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The created user.</returns>
    public override async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureRolesUsableAsync(request.RoleIds, cancellationToken);
        await EnsureScopeChangesAllowedAsync(request.Scopes, Array.Empty<ScopeGrant>(), cancellationToken);

        User entity = Mapper.ToEntity(request);
        entity.AuthUid = await _identityProvider.CreateAccountAsync(entity.Email, request.Password, entity.Name, cancellationToken);

        try
        {
            Repository.Add(entity);
            await UnitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await _identityProvider.DeleteAccountAsync(entity.AuthUid, CancellationToken.None);
            throw;
        }

        return Mapper.ToDto(entity);
    }

    /// <summary>
    /// Updates profile, roles, scopes and status; a status change is mirrored
    /// to the Firebase account so a deactivated user cannot sign in.
    /// </summary>
    /// <param name="id">User id.</param>
    /// <param name="request">Validated update request.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The updated user.</returns>
    public override async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        User entity = await LoadAsync(id, cancellationToken);
        bool wasActive = entity.IsActive;

        if (!request.IsActive)
        {
            EnsureNotSelf(id);
        }

        await EnsureRolesUsableAsync(request.RoleIds.Except(entity.RoleIds), cancellationToken);
        await EnsureScopeChangesAllowedAsync(request.Scopes, entity.Scopes, cancellationToken);

        Mapper.Apply(request, entity);
        Repository.Update(entity);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        if (wasActive != entity.IsActive)
        {
            await _identityProvider.SetDisabledAsync(entity.AuthUid, !entity.IsActive, cancellationToken);
        }

        return Mapper.ToDto(entity);
    }

    /// <summary>
    /// Deactivates the user and disables their Firebase account.
    /// </summary>
    /// <param name="id">User id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes once both are updated.</returns>
    public override async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureNotSelf(id);
        User entity = await LoadAsync(id, cancellationToken);

        await base.DeleteAsync(id, cancellationToken);
        await _identityProvider.SetDisabledAsync(entity.AuthUid, disabled: true, cancellationToken);
    }

    /// <summary>
    /// Prefix match on email (always stored lower-case).
    /// </summary>
    /// <param name="query">Org-scoped query.</param>
    /// <param name="search">Trimmed search text.</param>
    /// <returns>The filtered query, ordered by email.</returns>
    protected override Query ApplySearch(Query query, string search)
    {
        string field = FirestoreNaming.Field(nameof(User.Email));
        string prefix = search.ToLowerInvariant();
        return query.WhereStartsWith(field, prefix);
    }

    /// <summary>
    /// Sorts users by name.
    /// </summary>
    /// <param name="query">Org-scoped query.</param>
    /// <returns>The ordered query.</returns>
    protected override Query ApplyOrder(Query query) =>
        query.OrderBy(FirestoreNaming.Field(nameof(User.Name)));

    /// <summary>
    /// Checks that every role exists in the caller's organisation and is active.
    /// </summary>
    /// <param name="roleIds">Roles being newly assigned.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when all roles are usable.</returns>
    /// <exception cref="FieldValidationException">A role is unknown or inactive.</exception>
    private async Task EnsureRolesUsableAsync(IEnumerable<Guid> roleIds, CancellationToken cancellationToken)
    {
        List<Guid> requested = roleIds.Distinct().ToList();
        if (requested.Count == 0)
        {
            return;
        }

        IReadOnlyList<Role> found = await _roles.GetByIdsAsync(requested, cancellationToken);
        if (found.Count != requested.Count || found.Any(r => !r.IsActive))
        {
            throw new FieldValidationException(nameof(IUserFields.RoleIds), "One or more roles do not exist or are inactive.");
        }
    }

    /// <summary>
    /// Checks every scope grant being added or removed: the caller must hold that
    /// scope themselves (a global grant needs a global caller), and the object it
    /// names must exist in the organisation. Unchanged grants are not re-checked.
    /// </summary>
    /// <param name="requested">Scopes in the request.</param>
    /// <param name="current">Scopes the user holds now (empty when creating).</param>
    /// <param name="cancellationToken">Cancels the checks.</param>
    /// <returns>A task that completes when every change is allowed.</returns>
    /// <exception cref="ForbiddenException">The caller does not hold a scope being changed.</exception>
    /// <exception cref="FieldValidationException">A scope names an unknown object or an unsupported type.</exception>
    private async Task EnsureScopeChangesAllowedAsync(
        IEnumerable<ScopeGrantDto> requested, IEnumerable<ScopeGrant> current, CancellationToken cancellationToken)
    {
        var wanted = requested.Select(s => (s.ScopeType, s.ScopeId)).ToHashSet();
        var held = current.Select(s => (s.ScopeType, s.ScopeId)).ToHashSet();
        var changed = wanted.Except(held).Concat(held.Except(wanted)).ToList();

        foreach (var (scopeType, scopeId) in changed)
        {
            bool callerHolds = scopeType == ScopeType.Global
                ? await _permissions.GetScopeIdsAsync(ScopeType.Global, cancellationToken) is null
                : await _permissions.CoversAsync(scopeType, scopeId!.Value, cancellationToken);
            if (!callerHolds)
            {
                throw new ForbiddenException("You can only grant or remove scopes you hold yourself.");
            }
        }

        List<Guid> newWarehouseIds = wanted.Except(held)
            .Where(s => s.ScopeType == ScopeType.Warehouse)
            .Select(s => s.ScopeId!.Value)
            .ToList();
        if (newWarehouseIds.Count > 0
            && (await _warehouses.GetByIdsAsync(newWarehouseIds, cancellationToken)).Count != newWarehouseIds.Count)
        {
            throw new FieldValidationException(nameof(IUserFields.Scopes), "One or more warehouses do not exist.");
        }

        if (wanted.Except(held).Any(s => s.ScopeType is not (ScopeType.Global or ScopeType.Warehouse)))
        {
            throw new FieldValidationException(nameof(IUserFields.Scopes), "Only global and warehouse scopes are available so far.");
        }
    }

    /// <summary>
    /// Stops a user from deactivating themselves and locking the organisation out.
    /// </summary>
    /// <param name="id">User being deactivated.</param>
    /// <exception cref="BusinessRuleException">The caller is deactivating themselves.</exception>
    private void EnsureNotSelf(Guid id)
    {
        if (_currentUser.UserId == id)
        {
            throw new BusinessRuleException("You cannot deactivate your own account.");
        }
    }
}
