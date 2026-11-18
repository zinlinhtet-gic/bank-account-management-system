using bams.server.Models.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace bams.server.Data.Configurations;

public sealed class AuditorCashReturnConfiguration : IEntityTypeConfiguration<AuditorCashReturn>
{
    // Bounds custody metadata and indexes the daily manager queue.
    public void Configure(EntityTypeBuilder<AuditorCashReturn> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.HandoffIdsJson).HasColumnType("json");
        b.Property(x => x.Status).HasMaxLength(30);
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.DecisionNote).HasMaxLength(500);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => new { x.BusinessDate, x.Status });
    }
}
