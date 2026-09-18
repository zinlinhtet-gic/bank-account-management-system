using bams.server.Models.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace bams.server.Data.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.AccountNo).IsRequired().HasMaxLength(32);

        builder.HasIndex(a => a.AccountNo).IsUnique();

        builder.HasOne(a => a.AccountType)
            .WithMany()
            .HasForeignKey(a => a.AccountTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AccountHolderConfiguration : IEntityTypeConfiguration<AccountHolder>
{
    public void Configure(EntityTypeBuilder<AccountHolder> builder)
    {
        builder.HasKey(h => h.Id);

        builder.Property(h => h.OwnershipPercentage).HasPrecision(5, 2);
        builder.Property(h => h.SigningRule).HasMaxLength(100);
        builder.Property(h => h.Status).IsRequired().HasMaxLength(20);

        builder.HasOne(h => h.Account)
            .WithMany()
            .HasForeignKey(h => h.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.Customer)
            .WithMany()
            .HasForeignKey(h => h.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FixedDepositConfiguration : IEntityTypeConfiguration<FixedDeposit>
{
    public void Configure(EntityTypeBuilder<FixedDeposit> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.AppliedAnnualRate).HasPrecision(9, 4);
        builder.Property(f => f.Status).IsRequired().HasMaxLength(20);

        builder.HasIndex(f => new { f.AccountId, f.StartDate, f.MaturityDate }).IsUnique();

        builder.HasOne(f => f.Account)
            .WithMany()
            .HasForeignKey(f => f.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.InterestRateRule)
            .WithMany()
            .HasForeignKey(f => f.InterestRateRuleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.PayoutAccount)
            .WithMany()
            .HasForeignKey(f => f.PayoutAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AccountStatusHistoryConfiguration : IEntityTypeConfiguration<AccountStatusHistory>
{
    public void Configure(EntityTypeBuilder<AccountStatusHistory> builder)
    {
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Reason).HasMaxLength(300);

        builder.HasOne(h => h.Account)
            .WithMany()
            .HasForeignKey(h => h.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.ChangedByUser)
            .WithMany()
            .HasForeignKey(h => h.ChangedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
