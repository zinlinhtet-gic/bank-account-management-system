using bams.server.Models.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace bams.server.Data.Configurations;

public sealed class CorrectionProposalConfiguration : IEntityTypeConfiguration<CorrectionProposal>
{
    // Pending uniqueness is also checked under an exception row lock.
    public void Configure(EntityTypeBuilder<CorrectionProposal> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.Exception).WithMany().HasForeignKey(x => x.ExceptionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ExceptionId, x.Revision }).IsUnique();
        builder.HasIndex(x => new { x.Status, x.Verification });
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.Property(x => x.SourceHash).HasMaxLength(64);
        builder.Property(x => x.ReviewNote).HasMaxLength(3000);
    }
}
public sealed class EffectiveAccountBalanceConfiguration : IEntityTypeConfiguration<EffectiveAccountBalance>
{
    public void Configure(EntityTypeBuilder<EffectiveAccountBalance> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.AccountTransactionId, x.Id });
        builder.Property(x => x.ProposalSource).HasMaxLength(16).HasDefaultValue("Account");
        builder.HasIndex(x => new { x.ProposalSource, x.ProposalId, x.AccountTransactionId }).IsUnique();
    }
}
public sealed class DailySummaryRevisionConfiguration : IEntityTypeConfiguration<DailySummaryRevision>
{
    public void Configure(EntityTypeBuilder<DailySummaryRevision> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.BusinessDate, x.Revision }).IsUnique();
    }
}
