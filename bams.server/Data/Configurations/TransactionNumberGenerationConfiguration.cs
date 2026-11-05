using bams.server.Models.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace bams.server.Data.Configurations;

public sealed class TransactionNumberGenerationConfiguration : IEntityTypeConfiguration<TransactionNumberGeneration>
{
    public void Configure(EntityTypeBuilder<TransactionNumberGeneration> builder)
    {
        builder.HasKey(generation => generation.Id);
        builder.Property(generation => generation.BusinessDate).IsRequired().HasColumnType("date");
        builder.HasIndex(generation => generation.BusinessDate).IsUnique();
    }
}
