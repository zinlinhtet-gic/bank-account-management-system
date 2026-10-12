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
        builder.Property(job => job.LeaseToken).HasColumnType("varchar(36)").HasMaxLength(36);
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
        builder.Property(execution => execution.FailureSummary).HasMaxLength(500);
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
        builder.HasIndex(execution => new { execution.FinalFailureAtUtc, execution.FailureResolvedAtUtc })
            .HasDatabaseName("IX_SJExec_FinalFailure");
        builder.HasIndex(execution => execution.RetryRequestId).HasDatabaseName("IX_SJExec_RetryReqId");
        builder.HasIndex(execution => execution.FailureRetryRequestedBy).HasDatabaseName("IX_SJExec_RetryUserId");
        builder.HasOne<ScheduledJobRetryRequest>()
            .WithMany()
            .HasForeignKey(execution => execution.RetryRequestId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_SJExec_RetryReq");
        builder.HasOne<bams.server.Models.Security.User>()
            .WithMany()
            .HasForeignKey(execution => execution.FailureRetryRequestedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_SJExec_RetryUser");
    }
}

public sealed class ScheduledJobRetryRequestConfiguration : IEntityTypeConfiguration<bams.server.Models.Jobs.ScheduledJobRetryRequest>
{
    public void Configure(EntityTypeBuilder<bams.server.Models.Jobs.ScheduledJobRetryRequest> builder)
    {
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Status).HasConversion<string>().HasMaxLength(24);
        builder.HasIndex(request => new { request.ScheduledJobId, request.Status, request.ScheduledForUtc })
            .HasDatabaseName("IX_SJRetry_JobStatusDate");
        builder.HasIndex(request => request.FailedExecutionId).IsUnique()
            .HasDatabaseName("IX_SJRetry_FailedExec");
        builder.HasIndex(request => request.RequestedBy).HasDatabaseName("IX_SJRetry_RequestedBy");
        builder.HasOne(request => request.ScheduledJob).WithMany()
            .HasForeignKey(request => request.ScheduledJobId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_SJRetry_Job");
        builder.HasOne(request => request.FailedExecution).WithMany()
            .HasForeignKey(request => request.FailedExecutionId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_SJRetry_FailedExec");
        builder.HasOne<bams.server.Models.Security.User>().WithMany()
            .HasForeignKey(request => request.RequestedBy).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_SJRetry_User");
    }
}
