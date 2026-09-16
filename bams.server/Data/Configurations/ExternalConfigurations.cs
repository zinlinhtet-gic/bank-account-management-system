using bams.server.Models.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace bams.server.Data.Configurations;

public sealed class OtherBankConfiguration : IEntityTypeConfiguration<OtherBank>
{
    public void Configure(EntityTypeBuilder<OtherBank> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.BankCode).IsRequired().HasMaxLength(20);
        builder.Property(b => b.BankName).IsRequired().HasMaxLength(150);
        builder.Property(b => b.SwiftCode).HasMaxLength(20);
        builder.Property(b => b.Status).IsRequired().HasMaxLength(20);

        builder.HasIndex(b => b.BankCode).IsUnique();
    }
}

public sealed class ReconciliationBatchConfiguration : IEntityTypeConfiguration<ReconciliationBatch>
{
    public void Configure(EntityTypeBuilder<ReconciliationBatch> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.ReconciliationType).IsRequired().HasMaxLength(40);
        builder.Property(b => b.Status).IsRequired().HasMaxLength(20);

        builder.HasOne(b => b.PerformedByUser)
            .WithMany()
            .HasForeignKey(b => b.PerformedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ReconciliationItemConfiguration : IEntityTypeConfiguration<ReconciliationItem>
{
    public void Configure(EntityTypeBuilder<ReconciliationItem> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Status).IsRequired().HasMaxLength(20);
        builder.Property(i => i.Note).HasMaxLength(300);

        builder.HasOne(i => i.ReconciliationBatch)
            .WithMany()
            .HasForeignKey(i => i.ReconciliationBatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Transaction)
            .WithMany()
            .HasForeignKey(i => i.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.TransactionEntry)
            .WithMany()
            .HasForeignKey(i => i.TransactionEntryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
