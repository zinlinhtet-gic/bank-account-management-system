using bams.server.Constants;
using bams.server.Models.Accounts;
using bams.server.Models.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace bams.server.Data.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Version).IsConcurrencyToken();

        builder.Property(a => a.AccountNo).IsRequired().HasMaxLength(32);

        builder.HasIndex(a => a.AccountNo).IsUnique();

        builder.HasOne(a => a.AccountType)
            .WithMany()
            .HasForeignKey(a => a.AccountTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AccountNumberGenerationConfiguration : IEntityTypeConfiguration<AccountNumberGeneration>
{
    // Configures the persisted counter used to allocate account numbers per type and UTC hour.
    public void Configure(EntityTypeBuilder<AccountNumberGeneration> builder)
    {
        builder.HasKey(generation => generation.Id);

        builder.Property(generation => generation.GenerationPeriod)
            .IsRequired()
            .HasMaxLength(AccountConstants.AccountNumberTimestampFormat.Length);

        builder.HasIndex(generation => new
            {
                generation.AccountTypeId,
                generation.GenerationPeriod
            })
            .IsUnique();

        builder.HasOne<AccountType>()
            .WithMany()
            .HasForeignKey(generation => generation.AccountTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AccountDocumentConfiguration : IEntityTypeConfiguration<AccountDocument>
{
    public void Configure(EntityTypeBuilder<AccountDocument> builder)
    {
        builder.HasKey(document => document.Id);

        builder.Property(document => document.DocumentNumber)
            .HasMaxLength(DocumentConstants.DocumentNumberMaximumLength);
        builder.Property(document => document.OriginalFileName)
            .IsRequired()
            .HasMaxLength(DocumentConstants.OriginalFileNameMaximumLength);
        builder.Property(document => document.FileReference)
            .IsRequired()
            .HasMaxLength(DocumentConstants.FileReferenceMaximumLength);
        builder.Property(document => document.ContentType)
            .IsRequired()
            .HasMaxLength(DocumentConstants.ContentTypeMaximumLength);
        builder.Property(document => document.Status)
            .IsRequired()
            .HasMaxLength(DocumentConstants.StatusMaximumLength);

        builder.HasIndex(document => new { document.AccountId, document.DocumentType })
            .IsUnique();

        builder.HasOne(document => document.Account)
            .WithMany()
            .HasForeignKey(document => document.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AccountHolderConfiguration : IEntityTypeConfiguration<AccountHolder>
{
    public void Configure(EntityTypeBuilder<AccountHolder> builder)
    {
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Version).IsConcurrencyToken();

        builder.Property(h => h.OwnershipPercentage).HasPrecision(5, 2);
        builder.Property(h => h.SigningRule)
            .HasMaxLength(AccountConstants.AccountHolderSigningRuleMaximumLength);
        builder.Property(h => h.Status).IsRequired().HasMaxLength(20);

        builder.HasOne(h => h.Account)
            .WithMany(account => account.AccountHolders)
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

        builder.Property(f => f.Version).IsConcurrencyToken();

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

        builder.Property(h => h.Reason)
            .HasMaxLength(AccountConstants.AccountStatusReasonMaximumLength);

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
