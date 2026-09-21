using bams.server.Models.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace bams.server.Data.Configurations;

public sealed class AccountTypeConfiguration : IEntityTypeConfiguration<AccountType>
{
    public void Configure(EntityTypeBuilder<AccountType> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Code).IsRequired().HasMaxLength(40);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(150);
        builder.Property(t => t.Category).HasMaxLength(40);
        builder.Property(t => t.Status).IsRequired().HasMaxLength(20);

        builder.HasIndex(t => t.Code).IsUnique();

        builder.HasOne(t => t.RequiredProduct)
            .WithMany()
            .HasForeignKey(t => t.RequiredProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AccountTypeRequiredDocumentConfiguration
    : IEntityTypeConfiguration<AccountTypeRequiredDocument>
{
    public void Configure(EntityTypeBuilder<AccountTypeRequiredDocument> builder)
    {
        builder.HasKey(requirement => new
            {
                requirement.AccountTypeId,
                requirement.DocumentType
            });

        builder.HasOne(requirement => requirement.AccountType)
            .WithMany()
            .HasForeignKey(requirement => requirement.AccountTypeId)
            .OnDelete(DeleteBehavior.Cascade);

    }
}

public sealed class AccountTypeRequiredDocumentConfiguration
    : IEntityTypeConfiguration<AccountTypeRequiredDocument>
{
    public void Configure(EntityTypeBuilder<AccountTypeRequiredDocument> builder)
    {
        builder.HasKey(requirement => new
            {
                requirement.AccountTypeId,
                requirement.DocumentType
            });

        builder.HasOne(requirement => requirement.AccountType)
            .WithMany()
            .HasForeignKey(requirement => requirement.AccountTypeId)
            .OnDelete(DeleteBehavior.Cascade);

    }
}

public sealed class AccountTypeRequiredDocumentConfiguration
    : IEntityTypeConfiguration<AccountTypeRequiredDocument>
{
    public void Configure(EntityTypeBuilder<AccountTypeRequiredDocument> builder)
    {
        builder.HasKey(requirement => new
            {
                requirement.AccountTypeId,
                requirement.DocumentType
            });

        builder.HasOne(requirement => requirement.AccountType)
            .WithMany()
            .HasForeignKey(requirement => requirement.AccountTypeId)
            .OnDelete(DeleteBehavior.Cascade);

    }
}

public sealed class AccountTypeRequiredDocumentConfiguration
    : IEntityTypeConfiguration<AccountTypeRequiredDocument>
{
    public void Configure(EntityTypeBuilder<AccountTypeRequiredDocument> builder)
    {
        builder.HasKey(requirement => new
            {
                requirement.AccountTypeId,
                requirement.DocumentType
            });

        builder.HasOne(requirement => requirement.AccountType)
            .WithMany()
            .HasForeignKey(requirement => requirement.AccountTypeId)
            .OnDelete(DeleteBehavior.Cascade);

    }
}

public sealed class InterestRateRuleConfiguration : IEntityTypeConfiguration<InterestRateRule>
{
    public void Configure(EntityTypeBuilder<InterestRateRule> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.AnnualRate).HasPrecision(9, 4);
        builder.Property(r => r.EarlyWithdrawalRate).HasPrecision(9, 4);
        builder.Property(r => r.Status).IsRequired().HasMaxLength(20);

        builder.HasOne(r => r.AccountType)
            .WithMany()
            .HasForeignKey(r => r.AccountTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FeeRuleConfiguration : IEntityTypeConfiguration<FeeRule>
{
    public void Configure(EntityTypeBuilder<FeeRule> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Percentage).HasPrecision(9, 4);
        builder.Property(r => r.TaxRate).HasPrecision(9, 4);
        builder.Property(r => r.Status).IsRequired().HasMaxLength(20);

        builder.HasOne(r => r.AccountType)
            .WithMany()
            .HasForeignKey(r => r.AccountTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
