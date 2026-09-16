using bams.server.Models.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace bams.server.Data.Configurations;

public sealed class GlAccountConfiguration : IEntityTypeConfiguration<GlAccount>
{
    public void Configure(EntityTypeBuilder<GlAccount> builder)
    {
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Code).IsRequired().HasMaxLength(20);
        builder.Property(g => g.Name).IsRequired().HasMaxLength(150);
        builder.Property(g => g.Status).IsRequired().HasMaxLength(20);

        builder.HasIndex(g => g.Code).IsUnique();

        builder.HasOne(g => g.Parent)
            .WithMany()
            .HasForeignKey(g => g.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DailySummaryConfiguration : IEntityTypeConfiguration<DailySummary>
{
    public void Configure(EntityTypeBuilder<DailySummary> builder)
    {
        builder.HasKey(s => s.Id);

        builder.HasIndex(s => new { s.SummaryDate, s.GlAccountId }).IsUnique();

        builder.HasOne(s => s.GlAccount)
            .WithMany()
            .HasForeignKey(s => s.GlAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MonthlySummaryConfiguration : IEntityTypeConfiguration<MonthlySummary>
{
    public void Configure(EntityTypeBuilder<MonthlySummary> builder)
    {
        builder.HasKey(s => s.Id);

        builder.HasIndex(s => new { s.Year, s.Month, s.GlAccountId }).IsUnique();

        builder.HasOne(s => s.GlAccount)
            .WithMany()
            .HasForeignKey(s => s.GlAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
