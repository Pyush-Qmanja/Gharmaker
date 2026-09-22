using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Shared.Constants;
using Platform.Shared.Entities.Catalog;
using Platform.Shared.Entities.Identity;

namespace Platform.Api.Data.Configurations;

/// <summary>
/// Table mapping for <see cref="Brand"/>.
/// </summary>
public sealed class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    /// <summary>
    /// Configures columns and constraints.
    /// </summary>
    /// <param name="builder">Entity builder.</param>
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(FieldLengths.Name).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(FieldLengths.Slug).IsRequired();
        builder.Property(x => x.LogoUrl).HasMaxLength(FieldLengths.Url);
        builder.Property(x => x.ManufacturerName).HasMaxLength(FieldLengths.Name);

        builder.HasIndex(x => new { x.OrgId, x.Slug }).IsUnique();

        builder.HasOne<Organisation>().WithMany().HasForeignKey(x => x.OrgId).OnDelete(DeleteBehavior.Restrict);
    }
}
