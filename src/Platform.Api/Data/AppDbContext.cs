using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Platform.Api.Common;
using Platform.Shared.Entities.Catalog;
using Platform.Shared.Entities.Common;
using Platform.Shared.Entities.Identity;

namespace Platform.Api.Data;

/// <summary>
/// The EF Core unit of work for the platform. Besides mapping entities it owns
/// three cross-cutting rules so no repository or service has to remember them:
/// <list type="number">
/// <item>Audit fields (<c>CreatedAt</c>, <c>CreatedBy</c>, <c>UpdatedAt</c>, <c>UpdatedBy</c>) are stamped on save.</item>
/// <item><c>OrgId</c> is stamped on insert of every <see cref="IOrgScoped"/> entity.</item>
/// <item>Every query on an <see cref="IOrgScoped"/> entity is filtered to the caller's organisation.</item>
/// </list>
/// </summary>
public class AppDbContext : DbContext
{
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates the context.
    /// </summary>
    /// <param name="options">EF Core options (provider, naming convention).</param>
    /// <param name="currentUser">Caller identity used for audit and tenancy.</param>
    /// <param name="timeProvider">Clock used for audit timestamps.</param>
    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser, TimeProvider timeProvider)
        : base(options)
    {
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    /// <summary>Organisations (tenants).</summary>
    public DbSet<Organisation> Organisations => Set<Organisation>();

    /// <summary>Users who can sign in.</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>Material brands.</summary>
    public DbSet<Brand> Brands => Set<Brand>();

    /// <summary>
    /// Caller's organisation. Read by the global query filter; EF Core
    /// re-evaluates it per query, so it is never cached across requests.
    /// </summary>
    public Guid? CurrentOrgId => _currentUser.OrgId;

    /// <summary>
    /// Applies every <c>IEntityTypeConfiguration</c> in this assembly, then the
    /// conventions shared by all entities.
    /// </summary>
    /// <param name="modelBuilder">Model being built.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            Type clrType = entityType.ClrType;

            if (typeof(BaseEntity).IsAssignableFrom(clrType))
            {
                // Keys are UUID v7 generated in code, never by the database.
                modelBuilder.Entity(clrType).Property(nameof(BaseEntity.Id)).ValueGeneratedNever();
            }

            if (typeof(IOrgScoped).IsAssignableFrom(clrType))
            {
                modelBuilder.Entity(clrType).HasIndex(nameof(IOrgScoped.OrgId));
                ApplyOrgFilterMethod.MakeGenericMethod(clrType).Invoke(this, new object[] { modelBuilder });
            }
        }
    }

    /// <summary>
    /// Stamps audit and tenancy fields, then saves.
    /// </summary>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>Number of rows written.</returns>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAuditAndTenancy();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Stamps audit and tenancy fields, then saves. Synchronous saves are
    /// covered too so the rules cannot be bypassed.
    /// </summary>
    /// <param name="acceptAllChangesOnSuccess">See EF Core documentation.</param>
    /// <returns>Number of rows written.</returns>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampAuditAndTenancy();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <summary>Reflection handle for <see cref="ApplyOrgFilter{TEntity}"/>.</summary>
    private static readonly MethodInfo ApplyOrgFilterMethod =
        typeof(AppDbContext).GetMethod(nameof(ApplyOrgFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    /// <summary>
    /// Restricts every query on <typeparamref name="TEntity"/> to the caller's organisation.
    /// Use <c>IgnoreQueryFilters()</c> only for sign-in and seeding.
    /// </summary>
    /// <typeparam name="TEntity">An org-scoped entity.</typeparam>
    /// <param name="modelBuilder">Model being built.</param>
    private void ApplyOrgFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, IOrgScoped
    {
        Expression<Func<TEntity, bool>> filter = e => e.OrgId == CurrentOrgId;
        modelBuilder.Entity<TEntity>().HasQueryFilter(filter);
    }

    /// <summary>
    /// Sets audit fields on added and modified entities, sets <c>OrgId</c> on
    /// added org-scoped entities, and protects the insert-only fields from
    /// being overwritten by an update.
    /// </summary>
    private void StampAuditAndTenancy()
    {
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        Guid? userId = _currentUser.UserId;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy ??= userId;
                    if (entry.Entity is IOrgScoped scoped && scoped.OrgId == Guid.Empty)
                    {
                        scoped.OrgId = CurrentOrgId
                            ?? throw new InvalidOperationException(
                                $"Cannot insert {entry.Metadata.ClrType.Name} without an organisation.");
                    }
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    entry.Property(e => e.CreatedAt).IsModified = false;
                    entry.Property(e => e.CreatedBy).IsModified = false;
                    if (entry.Entity is IOrgScoped)
                    {
                        entry.Property(nameof(IOrgScoped.OrgId)).IsModified = false;
                    }
                    break;
            }
        }
    }
}
