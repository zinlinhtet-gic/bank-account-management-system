using bams.server.Models.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace bams.server.Data.Configurations;

public sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Code).IsRequired().HasMaxLength(20);
        builder.Property(b => b.Name).IsRequired().HasMaxLength(150);
        builder.Property(b => b.City).HasMaxLength(100);
        builder.Property(b => b.Status).IsRequired().HasMaxLength(20);

        builder.HasIndex(b => b.Code).IsUnique();
    }
}
