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
        Capabilities = request.Capabilities.Distinct().ToList(),
    };

    /// <inheritdoc />
    public void Apply(UpdateRoleRequest request, Role entity)
    {
        entity.Name = DtoMapping.Clean(request.Name);
        entity.Capabilities = request.Capabilities.Distinct().ToList();
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
        Scopes = entity.Scopes.Select(s => new ScopeGrantDto { ScopeType = s.ScopeType, ScopeId = s.ScopeId }).ToList(),
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
    };

    /// <inheritdoc />
    public void Apply(UpdateUserRequest request, User entity)
    {
        entity.Name = DtoMapping.Clean(request.Name);
        entity.Phone = DtoMapping.CleanOptional(request.Phone);
        entity.RoleIds = request.RoleIds.Distinct().ToList();
        entity.Scopes = ToScopes(request.Scopes);
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
}
