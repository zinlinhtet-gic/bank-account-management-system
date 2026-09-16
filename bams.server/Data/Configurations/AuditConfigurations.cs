using bams.server.Models.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace bams.server.Data.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Action).IsRequired().HasMaxLength(100);
        builder.Property(l => l.EntityType).IsRequired().HasMaxLength(100);
        builder.Property(l => l.EntityId).IsRequired().HasMaxLength(64);
        builder.Property(l => l.IpAddress).HasMaxLength(64);
        builder.Property(l => l.DeviceInfo).HasMaxLength(300);

        builder.HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
