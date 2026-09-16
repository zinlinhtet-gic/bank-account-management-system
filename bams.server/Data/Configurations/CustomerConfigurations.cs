using bams.server.Models.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace bams.server.Data.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.CustomerNo).IsRequired().HasMaxLength(32);
        builder.Property(c => c.FullName).IsRequired().HasMaxLength(150);
        builder.Property(c => c.Nationality).HasMaxLength(64);
        builder.Property(c => c.NrcNumber).HasMaxLength(32);
        builder.Property(c => c.PassportNumber).HasMaxLength(32);
        builder.Property(c => c.Phone).HasMaxLength(32);
        builder.Property(c => c.Occupation).HasMaxLength(100);
        builder.Property(c => c.AddressLine1).HasMaxLength(200);
        builder.Property(c => c.AddressLine2).HasMaxLength(200);
        builder.Property(c => c.City).HasMaxLength(100);
        builder.Property(c => c.State).HasMaxLength(100);
        builder.Property(c => c.PostalCode).HasMaxLength(20);
        builder.Property(c => c.Country).HasMaxLength(100);
        builder.Property(c => c.Email).HasMaxLength(256);
        builder.Property(c => c.Status).IsRequired().HasMaxLength(20);

        builder.HasIndex(c => c.CustomerNo).IsUnique();
    }
}

public sealed class CustomerDocumentConfiguration : IEntityTypeConfiguration<CustomerDocument>
{
    public void Configure(EntityTypeBuilder<CustomerDocument> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.DocumentNumber).HasMaxLength(64);
        builder.Property(d => d.FileReference).HasMaxLength(300);
        builder.Property(d => d.Status).IsRequired().HasMaxLength(20);

        builder.HasOne(d => d.Customer)
            .WithMany()
            .HasForeignKey(d => d.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.VerifiedByUser)
            .WithMany()
            .HasForeignKey(d => d.VerifiedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
