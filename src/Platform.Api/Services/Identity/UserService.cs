using Google.Cloud.Firestore;
using Platform.Api.Common;
using Platform.Api.Common.Exceptions;
using Platform.Api.Firestore;
using Platform.Api.Mapping;
using Platform.Api.Repositories;
using Platform.Api.Security;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Identity;
using Platform.Shared.Entities.Identity;

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

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="repository">User data access.</param>
    /// <param name="unitOfWork">Commits staged changes.</param>
    /// <param name="mapper">User mapping.</param>
    /// <param name="roles">Role data access, to validate assigned roles.</param>
    /// <param name="identityProvider">Firebase Authentication account management.</param>
    /// <param name="currentUser">Caller, to stop self-deactivation.</param>
    public UserService(
        IRepository<User> repository,
        IUnitOfWork unitOfWork,
        IEntityMapper<User, UserDto, CreateUserRequest, UpdateUserRequest> mapper,
        IRepository<Role> roles,
        IIdentityProvider identityProvider,
        ICurrentUser currentUser)
        : base(repository, unitOfWork, mapper)
    {
        _roles = roles;
        _identityProvider = identityProvider;
        _currentUser = currentUser;
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
        return query.WhereGreaterThanOrEqualTo(field, prefix).WhereLessThan(field, prefix + '').OrderBy(field);
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
