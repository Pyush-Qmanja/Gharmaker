using Google.Cloud.Firestore;
using Platform.Api.Common;
using Platform.Api.Security;
using Platform.Api.Services.Catalog.Import;
using Platform.Shared.Constants;
using Platform.Shared.Entities.Catalog;
using Platform.Shared.Entities.Identity;
using Platform.Shared.Entities.Pricing;
using Platform.Shared.Units;

namespace Platform.Api.Firestore;

/// <summary>
/// Development-only bootstrap. Makes sure the admin from the <c>Seed</c>
/// section exists as a Firebase Authentication account and a platform user,
/// with an organisation, an "Administrator" role holding every capability,
/// and global scope; that the organisation has the standard units; and, once,
/// a starter catalogue. Safe to run on every start; brings older data up to date.
/// </summary>
public static class DevelopmentSeeder
{
    /// <summary>Name of the seeded all-capabilities role.</summary>
    private const string AdminRoleName = "Administrator";

    /// <summary>Starter catalogue file under <c>Seed/</c>, loaded once into an empty organisation.</summary>
    private const string StarterCatalogueFile = "starter-catalogue.csv";

    /// <summary>Display name of the seeded admin user.</summary>
    private const string AdminUserName = "Administrator";

    /// <summary>Name of the seeded retail price list.</summary>
    private const string RetailListName = "Retail";

    /// <summary>Code of the seeded retail price list.</summary>
    private const string RetailListCode = "RETAIL";

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

        User admin;
        if (existing.Count == 0)
        {
            admin = await CreateOrganisationWithAdminAsync(context, configuration, email, authUid, now);
        }
        else
        {
            DocumentSnapshot userDocument = existing.Documents[0];
            admin = DocumentConverter.FromDocument<User>(userDocument);
            Role role = await EnsureAdminRoleAsync(context, admin.OrgId, now);
            await UpgradeAdminAsync(userDocument, admin, authUid, role.Id);
        }

        await EnsureStandardUomsAsync(context, admin.OrgId, now);
        await EnsureRetailPriceListAsync(context, admin.OrgId, now);
        await LoadStarterCatalogueAsync(scope.ServiceProvider, context, admin);
    }

    /// <summary>
    /// Adds any standard unit (<see cref="StandardUoms"/>) the organisation does not have yet.
    /// </summary>
    /// <param name="context">Database entry point.</param>
    /// <param name="orgId">Organisation id.</param>
    /// <param name="now">Creation time (UTC).</param>
    /// <returns>A task that completes when missing units are saved.</returns>
    private static async Task EnsureStandardUomsAsync(IFirestoreContext context, Guid orgId, DateTime now)
    {
        QuerySnapshot existing = await context.Collection<Uom>()
            .WhereEqualTo(FirestoreNaming.Field(nameof(Uom.OrgId)), DocumentConverter.ToFirestoreValue(orgId))
            .GetSnapshotAsync();
        var have = existing.Documents
            .Select(d => DocumentConverter.FromDocument<Uom>(d).Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        WriteBatch batch = context.Database.StartBatch();
        int added = 0;
        foreach (var (code, name, dimension, baseFactor) in StandardUoms.All.Where(u => !have.Contains(u.Code)))
        {
            var unit = new Uom { OrgId = orgId, Code = code, Name = name, Dimension = dimension, BaseFactor = baseFactor, CreatedAt = now };
            batch.Create(context.Collection<Uom>().Document(unit.Id.ToString()), DocumentConverter.ToDocument(unit));
            added++;
        }

        if (added > 0)
        {
            await batch.CommitAsync();
        }
    }

    /// <summary>
    /// Creates the retail price list if the organisation has none. Every store
    /// needs exactly one: customers without a tier or contract buy from it.
    /// Prices and GST rates are not seeded — they are the business's to set.
    /// </summary>
    /// <param name="context">Firestore access.</param>
    /// <param name="orgId">Organisation to seed.</param>
    /// <param name="now">Creation time.</param>
    /// <returns>A task that completes when the list exists.</returns>
    private static async Task EnsureRetailPriceListAsync(IFirestoreContext context, Guid orgId, DateTime now)
    {
        QuerySnapshot retail = await context.Collection<PriceList>()
            .WhereEqualTo(FirestoreNaming.Field(nameof(PriceList.OrgId)), DocumentConverter.ToFirestoreValue(orgId))
            .WhereEqualTo(FirestoreNaming.Field(nameof(PriceList.Type)), DocumentConverter.ToFirestoreValue(PriceListType.Retail))
            .Limit(1)
            .GetSnapshotAsync();
        if (retail.Count > 0)
        {
            return;
        }

        var list = new PriceList { OrgId = orgId, Name = RetailListName, Code = RetailListCode, Type = PriceListType.Retail, CreatedAt = now };
        await context.Collection<PriceList>().Document(list.Id.ToString()).CreateAsync(DocumentConverter.ToDocument(list));
    }

    /// <summary>
    /// Loads <c>Seed/starter-catalogue.csv</c> through the normal import, once:
    /// only when the organisation has no products yet. Runs as the admin so
    /// tenancy and audit fields are filled exactly as for a user import.
    /// </summary>
    /// <param name="services">Scoped services.</param>
    /// <param name="context">Database entry point.</param>
    /// <param name="admin">Seeded admin, whose organisation receives the catalogue.</param>
    /// <returns>A task that completes when the catalogue is loaded or skipped.</returns>
    private static async Task LoadStarterCatalogueAsync(IServiceProvider services, IFirestoreContext context, User admin)
    {
        QuerySnapshot anyProduct = await context.Collection<Product>()
            .WhereEqualTo(FirestoreNaming.Field(nameof(Product.OrgId)), DocumentConverter.ToFirestoreValue(admin.OrgId))
            .Limit(1)
            .GetSnapshotAsync();
        string path = Path.Combine(AppContext.BaseDirectory, "Seed", StarterCatalogueFile);
        if (anyProduct.Count > 0 || !File.Exists(path))
        {
            return;
        }

        using (SystemIdentity.Use(admin.OrgId, admin.Id))
        {
            await using FileStream file = File.OpenRead(path);
            await services.GetRequiredService<ICatalogImportService>().ImportAsync(file, StarterCatalogueFile);
        }
    }

    /// <summary>
    /// First run: creates the organisation, the administrator role and the admin user in one batch.
    /// </summary>
    /// <param name="context">Database entry point.</param>
    /// <param name="configuration">Reads the organisation name.</param>
    /// <param name="email">Admin email.</param>
    /// <param name="authUid">Admin's Firebase uid.</param>
    /// <param name="now">Creation time (UTC).</param>
    /// <returns>The new admin user.</returns>
    private static async Task<User> CreateOrganisationWithAdminAsync(
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
        return admin;
    }

    /// <summary>
    /// Finds the organisation's administrator role (the system top role, or on
    /// older data the role with the administrator name), creating it if missing,
    /// and keeps it marked as the system role, active, at the top of the
    /// hierarchy, and holding every capability.
    /// </summary>
    /// <param name="context">Database entry point.</param>
    /// <param name="orgId">Organisation id.</param>
    /// <param name="now">Timestamp for create/update (UTC).</param>
    /// <returns>The role.</returns>
    private static async Task<Role> EnsureAdminRoleAsync(IFirestoreContext context, Guid orgId, DateTime now)
    {
        Query roles = context.Collection<Role>()
            .WhereEqualTo(FirestoreNaming.Field(nameof(Role.OrgId)), DocumentConverter.ToFirestoreValue(orgId));
        QuerySnapshot found = await roles.WhereEqualTo(FirestoreNaming.Field(nameof(Role.IsSystem)), true).Limit(1).GetSnapshotAsync();
        if (found.Count == 0)
        {
            found = await roles.WhereEqualTo(FirestoreNaming.Field(nameof(Role.Name)), AdminRoleName).Limit(1).GetSnapshotAsync();
        }

        if (found.Count == 0)
        {
            Role created = NewAdminRole(orgId, now);
            await context.Collection<Role>().Document(created.Id.ToString()).CreateAsync(DocumentConverter.ToDocument(created));
            return created;
        }

        Role role = DocumentConverter.FromDocument<Role>(found.Documents[0]);
        List<string> all = Capabilities.All.Select(c => c.Code).ToList();
        if (!all.All(role.Capabilities.Contains) || !role.IsActive || !role.IsSystem || role.ParentId is not null)
        {
            await found.Documents[0].Reference.UpdateAsync(new Dictionary<string, object?>
            {
                [FirestoreNaming.Field(nameof(Role.Capabilities))] = DocumentConverter.ToFirestoreValue(all),
                [FirestoreNaming.Field(nameof(Role.IsActive))] = true,
                [FirestoreNaming.Field(nameof(Role.IsSystem))] = true,
                [FirestoreNaming.Field(nameof(Role.ParentId))] = null,
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
        IsSystem = true,
        Capabilities = Capabilities.All.Select(c => c.Code).ToList(),
        CreatedAt = now,
    };
}
