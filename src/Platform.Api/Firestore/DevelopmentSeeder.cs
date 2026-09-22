using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Identity;
using Platform.Shared.Entities.Identity;

namespace Platform.Api.Firestore;

/// <summary>
/// Development-only bootstrap: creates a first organisation and admin user so
/// a fresh database can be signed in to immediately. Does nothing when any
/// organisation already exists.
/// </summary>
public static class DevelopmentSeeder
{
    /// <summary>
    /// Seeds the first organisation and admin user from the <c>Seed</c> section.
    /// </summary>
    /// <param name="services">Root service provider.</param>
    /// <param name="configuration">App configuration holding the <c>Seed</c> section.</param>
    /// <returns>A task that completes when seeding is done.</returns>
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IFirestoreContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        QuerySnapshot existing = await context.Collection<Organisation>().Limit(1).GetSnapshotAsync();
        if (existing.Count > 0)
        {
            return;
        }

        string email = configuration["Seed:AdminEmail"]
            ?? throw new InvalidOperationException("Seed:AdminEmail is not configured.");
        string password = configuration["Seed:AdminPassword"]
            ?? throw new InvalidOperationException("Seed:AdminPassword is not configured.");

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        var organisation = new Organisation
        {
            Name = configuration["Seed:OrganisationName"] ?? "Default Organisation",
            CreatedAt = now,
        };
        var admin = new User
        {
            OrgId = organisation.Id,
            Name = "Administrator",
            Email = email.Trim().ToLowerInvariant(),
            CreatedAt = now,
        };
        admin.PasswordHash = hasher.HashPassword(admin, password);

        // Both documents in one atomic batch: never an organisation without its admin.
        WriteBatch batch = context.Database.StartBatch();
        batch.Create(context.Collection<Organisation>().Document(organisation.Id.ToString()), DocumentConverter.ToDocument(organisation));
        batch.Create(context.Collection<User>().Document(admin.Id.ToString()), DocumentConverter.ToDocument(admin));
        await batch.CommitAsync();
    }
}
