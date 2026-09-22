using Google.Cloud.Firestore;
using Platform.Api.Security;
using Platform.Shared.Entities.Identity;

namespace Platform.Api.Firestore;

/// <summary>
/// Development-only bootstrap: makes sure the admin from the <c>Seed</c>
/// section exists — as a Firebase Authentication account, as a platform user,
/// and with an organisation — so a fresh project can be signed in to at once.
/// Safe to run on every start.
/// </summary>
public static class DevelopmentSeeder
{
    /// <summary>
    /// Pre-Firebase-Auth field that held a password hash. Removed from any
    /// existing user document when it is linked to Firebase Authentication.
    /// </summary>
    private const string LegacyPasswordHashField = "password_hash";

    /// <summary>
    /// Ensures the seed admin and its organisation exist and are linked to Firebase Authentication.
    /// </summary>
    /// <param name="services">Root service provider.</param>
    /// <param name="configuration">App configuration holding the <c>Seed</c> section.</param>
    /// <returns>A task that completes when seeding is done.</returns>
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IFirestoreContext>();
        var identityProvider = scope.ServiceProvider.GetRequiredService<IIdentityProvider>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        string email = (configuration["Seed:AdminEmail"]
            ?? throw new InvalidOperationException("Seed:AdminEmail is not configured.")).Trim().ToLowerInvariant();
        string password = configuration["Seed:AdminPassword"]
            ?? throw new InvalidOperationException("Seed:AdminPassword is not configured.");
        const string adminName = "Administrator";

        string authUid = await identityProvider.EnsureAccountAsync(email, password, adminName);

        QuerySnapshot existing = await context.Collection<User>()
            .WhereEqualTo(FirestoreNaming.Field(nameof(User.Email)), email)
            .Limit(1)
            .GetSnapshotAsync();

        if (existing.Count > 0)
        {
            await LinkExistingUserAsync(existing.Documents[0], authUid);
            return;
        }

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        var organisation = new Organisation
        {
            Name = configuration["Seed:OrganisationName"] ?? "Default Organisation",
            CreatedAt = now,
        };
        var admin = new User
        {
            OrgId = organisation.Id,
            Name = adminName,
            Email = email,
            AuthUid = authUid,
            CreatedAt = now,
        };

        // Both documents in one atomic batch: never an organisation without its admin.
        WriteBatch batch = context.Database.StartBatch();
        batch.Create(context.Collection<Organisation>().Document(organisation.Id.ToString()), DocumentConverter.ToDocument(organisation));
        batch.Create(context.Collection<User>().Document(admin.Id.ToString()), DocumentConverter.ToDocument(admin));
        await batch.CommitAsync();
    }

    /// <summary>
    /// Links an existing user document to its Firebase account and removes the
    /// old password hash, if it is not linked already.
    /// </summary>
    /// <param name="userDocument">Existing user document.</param>
    /// <param name="authUid">Firebase Authentication uid.</param>
    /// <returns>A task that completes when the document is updated.</returns>
    private static async Task LinkExistingUserAsync(DocumentSnapshot userDocument, string authUid)
    {
        string authUidField = FirestoreNaming.Field(nameof(User.AuthUid));
        bool alreadyLinked = userDocument.TryGetValue(authUidField, out string current) && current == authUid;
        if (alreadyLinked && !userDocument.ContainsField(LegacyPasswordHashField))
        {
            return;
        }

        await userDocument.Reference.UpdateAsync(new Dictionary<string, object>
        {
            [authUidField] = authUid,
            [LegacyPasswordHashField] = FieldValue.Delete,
        });
    }
}
