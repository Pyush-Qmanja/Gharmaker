using Platform.Shared.Constants;
using Platform.Shared.Dtos.Identity;
using Platform.Shared.Entities.Identity;

namespace Platform.Api.Mapping.Identity;

/// <summary>
/// Maps <see cref="Role"/> to and from its DTOs.
/// </summary>
public sealed class RoleMapper : IEntityMapper<Role, RoleDto, CreateRoleRequest, UpdateRoleRequest>
{
    /// <inheritdoc />
    public RoleDto ToDto(Role entity) => new RoleDto
    {
        Name = entity.Name,
        Capabilities = entity.Capabilities.ToList(),
        IsActive = entity.IsActive,
    }.WithAuditFrom(entity);

    /// <inheritdoc />
    public Role ToEntity(CreateRoleRequest request) => new()
    {
        Name = DtoMapping.Clean(request.Name),
        Capabilities = Features.Complete(request.Capabilities),
    };

    /// <inheritdoc />
    public void Apply(UpdateRoleRequest request, Role entity)
    {
        entity.Name = DtoMapping.Clean(request.Name);
        entity.Capabilities = Features.Complete(request.Capabilities);
        entity.IsActive = request.IsActive;
    }
}

/// <summary>
/// Maps <see cref="User"/> to and from its DTOs. The Firebase account link
/// (<see cref="User.AuthUid"/>) is set by <c>UserService</c>, never from a request.
/// </summary>
public sealed class UserMapper : IEntityMapper<User, UserDto, CreateUserRequest, UpdateUserRequest>
{
    /// <inheritdoc />
    public UserDto ToDto(User entity) => new UserDto
    {
        Name = entity.Name,
        Email = entity.Email,
        Phone = entity.Phone,
        RoleIds = entity.RoleIds.ToList(),
        Scopes = ToScopeDtos(entity.Scopes),
        Access = entity.Access.Select(a => new FeatureAccessDto
        {
            Feature = a.Feature,
            Level = a.Level,
            Scopes = ToScopeDtos(a.Scopes),
        }).ToList(),
        IsActive = entity.IsActive,
    }.WithAuditFrom(entity);

    /// <inheritdoc />
    public User ToEntity(CreateUserRequest request) => new()
    {
        Name = DtoMapping.Clean(request.Name),
        Email = request.Email.Trim().ToLowerInvariant(),
        Phone = DtoMapping.CleanOptional(request.Phone),
        RoleIds = request.RoleIds.Distinct().ToList(),
        Scopes = ToScopes(request.Scopes),
        Access = ToAccess(request.Access),
    };

    /// <inheritdoc />
    public void Apply(UpdateUserRequest request, User entity)
    {
        entity.Name = DtoMapping.Clean(request.Name);
        entity.Phone = DtoMapping.CleanOptional(request.Phone);
        entity.RoleIds = request.RoleIds.Distinct().ToList();
        entity.Scopes = ToScopes(request.Scopes);
        entity.Access = ToAccess(request.Access);
        entity.IsActive = request.IsActive;
    }

    /// <summary>
    /// Converts requested scopes to stored grants, dropping exact duplicates.
    /// </summary>
    /// <param name="scopes">Requested scopes.</param>
    /// <returns>Grants to store.</returns>
    private static List<ScopeGrant> ToScopes(IEnumerable<ScopeGrantDto> scopes) =>
        scopes
            .DistinctBy(s => (s.ScopeType, s.ScopeId))
            .Select(s => new ScopeGrant { ScopeType = s.ScopeType, ScopeId = s.ScopeId })
            .ToList();

    /// <summary>
    /// Converts stored grants to their DTOs.
    /// </summary>
    /// <param name="scopes">Stored grants.</param>
    /// <returns>The DTOs.</returns>
    private static List<ScopeGrantDto> ToScopeDtos(IEnumerable<ScopeGrant> scopes) =>
        scopes.Select(s => new ScopeGrantDto { ScopeType = s.ScopeType, ScopeId = s.ScopeId }).ToList();

    /// <summary>
    /// Converts requested feature access to stored rows, in canonical form
    /// (no "no access" rows, one row per feature, scopes only where they apply).
    /// </summary>
    /// <param name="access">Requested rows.</param>
    /// <returns>Rows to store.</returns>
    private static List<FeatureAccess> ToAccess(IEnumerable<FeatureAccessDto> access) =>
        UserFieldsNormaliser.Normalise(access)
            .Select(a => new FeatureAccess { Feature = a.Feature, Level = a.Level, Scopes = ToScopes(a.Scopes) })
            .ToList();
}
