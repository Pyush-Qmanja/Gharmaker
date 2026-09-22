using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Platform.Shared.Entities.Identity;

namespace Platform.Api.Data;

/// <summary>
/// Development-only bootstrap: applies migrations and creates a first
/// organisation and admin user so a fresh clone can sign in immediately.
/// </summary>
public static class DbSeeder
{
    /// <summary>
    /// Migrates the database and seeds the first organisation and admin user
    /// from the <c>Seed</c> configuration section, if no organisation exists.
    /// </summary>
    /// <param name="services">Root service provider.</param>
    /// <param name="configuration">App configuration holding the <c>Seed</c> section.</param>
    /// <returns>A task that completes when seeding is done.</returns>
    public static async Task SeedDevelopmentAsync(IServiceProvider services, IConfiguration configuration)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        await context.Database.MigrateAsync();

        if (await context.Organisations.AnyAsync())
        {
            return;
        }

        string email = configuration["Seed:AdminEmail"]
            ?? throw new InvalidOperationException("Seed:AdminEmail is not configured.");
        string password = configuration["Seed:AdminPassword"]
            ?? throw new InvalidOperationException("Seed:AdminPassword is not configured.");

        var organisation = new Organisation { Name = configuration["Seed:OrganisationName"] ?? "Default Organisation" };
        var admin = new User
        {
            OrgId = organisation.Id,
            Name = "Administrator",
            Email = email.Trim().ToLowerInvariant(),
        };
        admin.PasswordHash = hasher.HashPassword(admin, password);

        context.Organisations.Add(organisation);
        context.Users.Add(admin);
        await context.SaveChangesAsync();
    }
}
