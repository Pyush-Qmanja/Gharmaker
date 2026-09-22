using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Shared.Constants;
using Platform.Shared.Entities.Identity;

namespace Platform.Api.Data.Configurations;

/// <summary>
/// Table mapping for <see cref="Organisation"/>.
/// </summary>
public sealed class OrganisationConfiguration : IEntityTypeConfiguration<Organisation>
{
    /// <summary>
    /// Configures columns and constraints.
    /// </summary>
    /// <param name="builder">Entity builder.</param>
    public void Configure(EntityTypeBuilder<Organisation> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(FieldLengths.Name).IsRequired();
    }
}

/// <summary>
/// Table mapping for <see cref="User"/>.
/// </summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <summary>
    /// Configures columns and constraints.
    /// </summary>
    /// <param name="builder">Entity builder.</param>
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(FieldLengths.Name).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(FieldLengths.Email).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(FieldLengths.Phone);
        builder.Property(x => x.PasswordHash).HasMaxLength(FieldLengths.PasswordHash).IsRequired();

        // Email is the login name, so it is unique across the whole platform.
        builder.HasIndex(x => x.Email).IsUnique();

        builder.HasOne<Organisation>().WithMany().HasForeignKey(x => x.OrgId).OnDelete(DeleteBehavior.Restrict);
    }
}
