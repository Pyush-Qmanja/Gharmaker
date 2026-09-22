using Google.Cloud.Firestore;
using Platform.Api.Security;
using Platform.Shared.Constants;
using Platform.Shared.Entities.Identity;

namespace Platform.Api.Firestore;

/// <summary>
/// Development-only bootstrap. Makes sure the admin from the <c>Seed</c>
/// section exists as a Firebase Authentication account and a platform user,
/// with an organisation, an "Administrator" role holding every capability,
/// and global scope. Safe to run on every start; brings older data up to date.
/// </summary>
public static class DevelopmentSeeder
{
    /// <summary>Name of the seeded all-capabilities role.</summary>
    private const string AdminRoleName = "Administrator";

    /// <summary>Display name of the seeded admin user.</summary>
    private const string AdminUserName = "Administrator";

    /// <summary>
    /// Pre-Firebase-Auth field that held a password hash; removed when found.
    /// </summary>
    private const string LegacyPasswordHashField = "password_hash";

    /// <summary>
    /// Ensures the seed admin, organisation and administrator role exist and are linked.
    /// </summary>
    /// <param name="services">Root service provider.</param>
    /// <param name="configuration">App configuration holding the <c>Seed</c> section.</param>
    /// <returns>A task that completes when seeding is done.</returns>
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IFirestoreContext>();
        var identityProvider = scope.ServiceProvider.GetRequiredService<IIdentityProvider>();
        DateTime now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;

        string email = (configuration["Seed:AdminEmail"]
            ?? throw new InvalidOperationException("Seed:AdminEmail is not configured.")).Trim().ToLowerInvariant();
        string password = configuration["Seed:AdminPassword"]
            ?? throw new InvalidOperationException("Seed:AdminPassword is not configured.");

        string authUid = await identityProvider.EnsureAccountAsync(email, password, AdminUserName);

        QuerySnapshot existing = await context.Collection<User>()
            .WhereEqualTo(FirestoreNaming.Field(nameof(User.Email)), email)
            .Limit(1)
            .GetSnapshotAsync();

        if (existing.Count == 0)
        {
            await CreateOrganisationWithAdminAsync(context, configuration, email, authUid, now);
            return;
        }

        DocumentSnapshot userDocument = existing.Documents[0];
        User admin = DocumentConverter.FromDocument<User>(userDocument);
        Role role = await EnsureAdminRoleAsync(context, admin.OrgId, now);
        await UpgradeAdminAsync(userDocument, admin, authUid, role.Id);
    }

    /// <summary>
    /// First run: creates the organisation, the administrator role and the admin user in one batch.
    /// </summary>
    /// <param name="context">Database entry point.</param>
    /// <param name="configuration">Reads the organisation name.</param>
    /// <param name="email">Admin email.</param>
    /// <param name="authUid">Admin's Firebase uid.</param>
    /// <param name="now">Creation time (UTC).</param>
    /// <returns>A task that completes when committed.</returns>
    private static async Task CreateOrganisationWithAdminAsync(
        IFirestoreContext context, IConfiguration configuration, string email, string authUid, DateTime now)
    {
        var organisation = new Organisation
        {
            Name = configuration["Seed:OrganisationName"] ?? "Default Organisation",
            CreatedAt = now,
        };
        Role role = NewAdminRole(organisation.Id, now);
        var admin = new User
        {
            OrgId = organisation.Id,
            Name = AdminUserName,
            Email = email,
            AuthUid = authUid,
            RoleIds = new List<Guid> { role.Id },
            Scopes = new List<ScopeGrant> { new() { ScopeType = ScopeType.Global } },
            CreatedAt = now,
        };

        // All three in one atomic batch: never an organisation without its admin.
        WriteBatch batch = context.Database.StartBatch();
        batch.Create(context.Collection<Organisation>().Document(organisation.Id.ToString()), DocumentConverter.ToDocument(organisation));
        batch.Create(context.Collection<Role>().Document(role.Id.ToString()), DocumentConverter.ToDocument(role));
        batch.Create(context.Collection<User>().Document(admin.Id.ToString()), DocumentConverter.ToDocument(admin));
        await batch.CommitAsync();
    }

    /// <summary>
    /// Finds the organisation's administrator role, creating it if missing and
    /// topping it up with any capability added since it was created.
    /// </summary>
    /// <param name="context">Database entry point.</param>
    /// <param name="orgId">Organisation id.</param>
    /// <param name="now">Timestamp for create/update (UTC).</param>
    /// <returns>The role.</returns>
    private static async Task<Role> EnsureAdminRoleAsync(IFirestoreContext context, Guid orgId, DateTime now)
    {
        QuerySnapshot found = await context.Collection<Role>()
            .WhereEqualTo(FirestoreNaming.Field(nameof(Role.OrgId)), DocumentConverter.ToFirestoreValue(orgId))
            .WhereEqualTo(FirestoreNaming.Field(nameof(Role.Name)), AdminRoleName)
            .Limit(1)
            .GetSnapshotAsync();

        if (found.Count == 0)
        {
            Role created = NewAdminRole(orgId, now);
            await context.Collection<Role>().Document(created.Id.ToString()).CreateAsync(DocumentConverter.ToDocument(created));
            return created;
        }

        Role role = DocumentConverter.FromDocument<Role>(found.Documents[0]);
        List<string> all = Capabilities.All.Select(c => c.Code).ToList();
        if (!all.All(role.Capabilities.Contains) || !role.IsActive)
        {
            await found.Documents[0].Reference.UpdateAsync(new Dictionary<string, object?>
            {
                [FirestoreNaming.Field(nameof(Role.Capabilities))] = DocumentConverter.ToFirestoreValue(all),
                [FirestoreNaming.Field(nameof(Role.IsActive))] = true,
                [FirestoreNaming.Field(nameof(Role.UpdatedAt))] = DocumentConverter.ToFirestoreValue(now),
            });
        }

        return role;
    }

    /// <summary>
    /// Brings an existing admin document up to date: Firebase link, the
    /// administrator role, global scope, and no legacy password hash.
    /// </summary>
    /// <param name="userDocument">Stored admin document.</param>
    /// <param name="admin">Admin as loaded.</param>
    /// <param name="authUid">Admin's Firebase uid.</param>
    /// <param name="adminRoleId">Administrator role id.</param>
    /// <returns>A task that completes when updated (or immediately if nothing to do).</returns>
    private static async Task UpgradeAdminAsync(DocumentSnapshot userDocument, User admin, string authUid, Guid adminRoleId)
    {
        bool upToDate = admin.AuthUid == authUid
            && admin.RoleIds.Contains(adminRoleId)
            && admin.Scopes.Any(s => s.ScopeType == ScopeType.Global)
            && !userDocument.ContainsField(LegacyPasswordHashField);
        if (upToDate)
        {
            return;
        }

        List<Guid> roleIds = admin.RoleIds.Append(adminRoleId).Distinct().ToList();
        List<ScopeGrant> scopes = admin.Scopes.Any(s => s.ScopeType == ScopeType.Global)
            ? admin.Scopes
            : admin.Scopes.Append(new ScopeGrant { ScopeType = ScopeType.Global }).ToList();

        await userDocument.Reference.UpdateAsync(new Dictionary<string, object?>
        {
            [FirestoreNaming.Field(nameof(User.AuthUid))] = authUid,
            [FirestoreNaming.Field(nameof(User.RoleIds))] = DocumentConverter.ToFirestoreValue(roleIds),
            [FirestoreNaming.Field(nameof(User.Scopes))] = DocumentConverter.ToFirestoreValue(scopes),
            [LegacyPasswordHashField] = FieldValue.Delete,
        });
    }

    /// <summary>
    /// Builds the administrator role holding every capability in the catalogue.
    /// </summary>
    /// <param name="orgId">Owning organisation.</param>
    /// <param name="now">Creation time (UTC).</param>
    /// <returns>A new, unsaved role.</returns>
    private static Role NewAdminRole(Guid orgId, DateTime now) => new()
    {
        OrgId = orgId,
        Name = AdminRoleName,
        Capabilities = Capabilities.All.Select(c => c.Code).ToList(),
        CreatedAt = now,
    };
}
