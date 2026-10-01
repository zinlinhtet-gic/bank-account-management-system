using bams.server.Models.InterestFees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace bams.server.Data.Configurations;

public sealed class InterestAccrualConfiguration : IEntityTypeConfiguration<InterestAccrual>
{
    public void Configure(EntityTypeBuilder<InterestAccrual> builder)
    {
        builder.HasKey(a => a.Id);
        builder.HasIndex(a => new { a.AccountId, a.PeriodStart, a.PeriodEnd }).IsUnique();

        builder.Property(a => a.AnnualRate).HasPrecision(9, 4);
        builder.Property(a => a.Status).IsRequired().HasMaxLength(20);
        builder.HasIndex(a => a.AccruedTransactionId).IsUnique();

        builder.HasOne(a => a.Account)
            .WithMany()
            .HasForeignKey(a => a.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.InterestRateRule)
            .WithMany()
            .HasForeignKey(a => a.InterestRateRuleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.PostedTransaction)
            .WithMany()
            .HasForeignKey(a => a.PostedTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.AccruedTransaction)
            .WithMany()
            .HasForeignKey(a => a.AccruedTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FeeAccrualConfiguration : IEntityTypeConfiguration<FeeAccrual>
{
    public void Configure(EntityTypeBuilder<FeeAccrual> builder)
    {
        builder.HasKey(a => a.Id);

        builder.HasIndex(a => new { a.AccountId, a.FeeType, a.PeriodStart, a.PeriodEnd }).IsUnique();
        builder.HasIndex(a => a.AccruedTransactionId).IsUnique();

        builder.HasOne(a => a.Account)
            .WithMany()
            .HasForeignKey(a => a.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.FeeRule)
            .WithMany()
            .HasForeignKey(a => a.FeeRuleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.PostedTransaction)
            .WithMany()
            .HasForeignKey(a => a.PostedTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.AccruedTransaction)
            .WithMany()
            .HasForeignKey(a => a.AccruedTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
