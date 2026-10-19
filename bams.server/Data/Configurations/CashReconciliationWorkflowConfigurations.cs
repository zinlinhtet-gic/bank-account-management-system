using bams.server.Models.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace bams.server.Data.Configurations;

public sealed class CashCorrectionProposalConfiguration : IEntityTypeConfiguration<CashCorrectionProposal>
{
    public void Configure(EntityTypeBuilder<CashCorrectionProposal> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Verification).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.InputsJson).HasColumnType("json");
        b.Property(x => x.SourceHash).HasMaxLength(64);
        b.Property(x => x.ReviewNote).HasMaxLength(2000);
        b.Property(x => x.VerificationNote).HasMaxLength(2000);
        b.HasIndex(x => new { x.ExceptionId, x.Status });
        b.HasOne(x => x.Exception).WithMany().HasForeignKey(x => x.ExceptionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ReconciliationExclusionConfiguration : IEntityTypeConfiguration<ReconciliationExclusion>
{
    public void Configure(EntityTypeBuilder<ReconciliationExclusion> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.ExceptionId);
        b.Property(x => x.SourceHash).HasMaxLength(64);
        b.Property(x => x.SourceIdsJson).HasColumnType("json");
        b.Property(x => x.Reason).HasMaxLength(2000);
        b.HasOne(x => x.Exception).WithMany().HasForeignKey(x => x.ExceptionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ReconciliationExcludedSourceConfiguration : IEntityTypeConfiguration<ReconciliationExcludedSource>
{
    public void Configure(EntityTypeBuilder<ReconciliationExcludedSource> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ExclusionId, x.SourceType, x.SourceId }).IsUnique();
        b.Property(x => x.SourceType).HasMaxLength(30);
        b.HasOne(x => x.Exclusion).WithMany(x => x.Sources).HasForeignKey(x => x.ExclusionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BankExpenseRequestConfiguration : IEntityTypeConfiguration<BankExpenseRequest>
{
    public void Configure(EntityTypeBuilder<BankExpenseRequest> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Payee).HasMaxLength(200);
        b.Property(x => x.Purpose).HasMaxLength(500);
        b.Property(x => x.ReceiptFileName).HasMaxLength(255);
        b.Property(x => x.ReceiptContentType).HasMaxLength(100);
        b.Property(x => x.ReceiptData).HasColumnType("longblob");
        b.Property(x => x.MissingReceiptReason).HasMaxLength(500);
        b.Property(x => x.Status).HasMaxLength(30);
        b.Property(x => x.ReviewNote).HasMaxLength(2000);
        b.HasIndex(x => new { x.BusinessDate, x.Status });
        b.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Transaction).WithMany().HasForeignKey(x => x.TransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}
