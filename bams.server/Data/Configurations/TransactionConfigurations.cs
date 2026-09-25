using bams.server.Models.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace bams.server.Data.Configurations;

public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TransactionNo).IsRequired().HasMaxLength(40);
        builder.Property(t => t.Description).HasMaxLength(300);
        builder.Property(t => t.ReferenceNo).HasMaxLength(100);

        builder.Property(t => t.IdempotencyKey).HasMaxLength(64);

        builder.HasIndex(t => t.TransactionNo).IsUnique();
        builder.HasIndex(t => t.IdempotencyKey).IsUnique();

        builder.HasOne(t => t.InitiatedByUser)
            .WithMany()
            .HasForeignKey(t => t.InitiatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.AuthorizedByUser)
            .WithMany()
            .HasForeignKey(t => t.AuthorizedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.PostedByUser)
            .WithMany()
            .HasForeignKey(t => t.PostedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.ReversalOfTransaction)
            .WithMany()
            .HasForeignKey(t => t.ReversalOfTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TransactionEntryConfiguration : IEntityTypeConfiguration<TransactionEntry>
{
    public void Configure(EntityTypeBuilder<TransactionEntry> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Description).HasMaxLength(300);

        builder.HasOne(e => e.Transaction)
            .WithMany()
            .HasForeignKey(e => e.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.GlAccount)
            .WithMany()
            .HasForeignKey(e => e.GlAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.CustomerAccount)
            .WithMany()
            .HasForeignKey(e => e.CustomerAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AccountTransactionConfiguration : IEntityTypeConfiguration<AccountTransaction>
{
    public void Configure(EntityTypeBuilder<AccountTransaction> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Description).HasMaxLength(300);
        builder.Property(t => t.ReferenceNo).HasMaxLength(100);
        builder.Property(t => t.Status).IsRequired().HasMaxLength(20);

        builder.HasOne(t => t.Transaction)
            .WithMany()
            .HasForeignKey(t => t.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Account)
            .WithMany()
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class NrcCashTransferDetailConfiguration : IEntityTypeConfiguration<NrcCashTransferDetail>
{
    public void Configure(EntityTypeBuilder<NrcCashTransferDetail> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.SenderName).IsRequired().HasMaxLength(150);
        builder.Property(d => d.SenderNrc).IsRequired().HasMaxLength(32);
        builder.Property(d => d.SenderPhone).HasMaxLength(32);
        builder.Property(d => d.ReceiverName).IsRequired().HasMaxLength(150);
        builder.Property(d => d.ReceiverNrc).IsRequired().HasMaxLength(32);
        builder.Property(d => d.ReceiverPhone).HasMaxLength(32);
        builder.Property(d => d.DeliveryType).IsRequired().HasMaxLength(30);
        builder.Property(d => d.PickupCodeHash).HasMaxLength(256);
        builder.Property(d => d.FailedPickupAttempts).HasDefaultValue(0);
        builder.Property(d => d.Status).IsRequired().HasMaxLength(20);

        builder.HasIndex(d => d.TransactionId).IsUnique();

        builder.HasOne(d => d.Transaction)
            .WithMany()
            .HasForeignKey(d => d.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.DestinationAccount)
            .WithMany()
            .HasForeignKey(d => d.DestinationAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.PickupBranch)
            .WithMany()
            .HasForeignKey(d => d.PickupBranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.PickupOtherBank)
            .WithMany()
            .HasForeignKey(d => d.PickupOtherBankId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.PickupVerifiedByUser)
            .WithMany()
            .HasForeignKey(d => d.PickupVerifiedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InterbankTransferDetailConfiguration : IEntityTypeConfiguration<InterbankTransferDetail>
{
    public void Configure(EntityTypeBuilder<InterbankTransferDetail> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.DestinationAccountNo).IsRequired().HasMaxLength(40);
        builder.Property(d => d.BeneficiaryName).IsRequired().HasMaxLength(150);
        builder.Property(d => d.GatewayReference).HasMaxLength(100);
        builder.Property(d => d.SettlementReference).HasMaxLength(100);

        builder.HasIndex(d => d.TransactionId).IsUnique();

        builder.HasOne(d => d.Transaction)
            .WithMany()
            .HasForeignKey(d => d.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.OtherBank)
            .WithMany()
            .HasForeignKey(d => d.OtherBankId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
