using bams.server.Models.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace bams.server.Data.Configurations;

public sealed class ScheduledJobConfiguration : IEntityTypeConfiguration<ScheduledJob>
{
    public void Configure(EntityTypeBuilder<ScheduledJob> builder)
    {
        builder.HasKey(job => job.Id);
        builder.Property(job => job.JobKey).IsRequired().HasMaxLength(100);
        builder.Property(job => job.DisplayName).IsRequired().HasMaxLength(160);
        builder.Property(job => job.ScheduleType).HasConversion<string>().HasMaxLength(20);
        builder.Property(job => job.TimeZoneId).IsRequired().HasMaxLength(100);
        builder.Property(job => job.LocalTime).HasColumnType("time(6)");
        builder.Property(job => job.Status).HasConversion<string>().HasMaxLength(24);
        builder.Property(job => job.LeaseToken).HasColumnType("char(36)").HasMaxLength(36);
        builder.HasIndex(job => job.JobKey).IsUnique();
        builder.HasIndex(job => new { job.IsEnabled, job.NextRunAtUtc, job.LeaseUntilUtc });
    }
}

public sealed class ScheduledJobExecutionConfiguration : IEntityTypeConfiguration<ScheduledJobExecution>
{
    public void Configure(EntityTypeBuilder<ScheduledJobExecution> builder)
    {
        builder.HasKey(execution => execution.Id);
        builder.Property(execution => execution.Status).HasConversion<string>().HasMaxLength(24);
        builder.Property(execution => execution.ErrorMessage).HasMaxLength(4000);
        builder.HasIndex(execution => new
        {
            execution.ScheduledJobId,
            execution.ScheduledForUtc,
            execution.AttemptNumber
        }).IsUnique();
        builder.HasOne(execution => execution.ScheduledJob)
            .WithMany(job => job.Executions)
            .HasForeignKey(execution => execution.ScheduledJobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
