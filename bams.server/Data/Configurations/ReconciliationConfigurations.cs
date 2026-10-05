using bams.server.Models.Accounting;
using bams.server.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace bams.server.Data.Configurations;

public sealed class BusinessDateConfiguration : IEntityTypeConfiguration<BusinessDate>
{
    public void Configure(EntityTypeBuilder<BusinessDate> builder)
    {
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => item.Date).IsUnique();
        builder.HasIndex(item => item.Status);
        builder.Property(item => item.Status).IsRequired().HasMaxLength(20);
    }
}

public sealed class AccountReconciliationRunConfiguration : IEntityTypeConfiguration<AccountReconciliationRun>
{
    public void Configure(EntityTypeBuilder<AccountReconciliationRun> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Status).IsRequired().HasMaxLength(30);
        builder.HasIndex(item => new { item.FromDate, item.ToDate, item.PerformedAtUtc });
        builder.HasIndex(item => item.ScheduledJobExecutionId).IsUnique();
        builder.HasOne<bams.server.Models.Accounts.Account>().WithMany().HasForeignKey(item => item.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<bams.server.Models.Jobs.ScheduledJobExecution>().WithMany().HasForeignKey(item => item.ScheduledJobExecutionId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ARR_ScheduledExec");
    }
}

public sealed class AccountReconciliationResultConfiguration : IEntityTypeConfiguration<AccountReconciliationResult>
{
    public void Configure(EntityTypeBuilder<AccountReconciliationResult> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Status).IsRequired().HasMaxLength(30);
        builder.HasIndex(item => new { item.AccountId, item.BusinessDate });
        builder.HasOne(item => item.Run).WithMany(run => run.Results).HasForeignKey(item => item.RunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Account).WithMany().HasForeignKey(item => item.AccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ReconciliationExceptionConfiguration : IEntityTypeConfiguration<ReconciliationException>
{
    public void Configure(EntityTypeBuilder<ReconciliationException> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Type).IsRequired().HasMaxLength(40);
        builder.Property(item => item.Source).IsRequired().HasMaxLength(40);
        builder.Property(item => item.Severity).IsRequired().HasMaxLength(20);
        builder.Property(item => item.Status).IsRequired().HasMaxLength(30);
        builder.Property(item => item.Notes).HasMaxLength(2000);
        builder.HasIndex(item => new { item.BusinessDate, item.Status, item.Severity });
        builder.HasIndex(item => new { item.AccountId, item.BusinessDate, item.Status });
        builder.HasOne(item => item.Account).WithMany().HasForeignKey(item => item.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.AssignedUser).WithMany().HasForeignKey(item => item.AssignedTo).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.RelatedTransaction).WithMany().HasForeignKey(item => item.RelatedTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.CorrectionTransaction).WithMany().HasForeignKey(item => item.CorrectionTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.PositionSession).WithMany().HasForeignKey(item => item.PositionSessionId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_RE_PositionSession");
        builder.HasOne<bams.server.Models.Security.User>().WithMany().HasForeignKey(item => item.CreatedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ReconciliationExceptionHistoryConfiguration : IEntityTypeConfiguration<ReconciliationExceptionHistory>
{
    public void Configure(EntityTypeBuilder<ReconciliationExceptionHistory> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.OldStatus).IsRequired().HasMaxLength(30);
        builder.Property(item => item.NewStatus).IsRequired().HasMaxLength(30);
        builder.Property(item => item.Note).HasMaxLength(2000);
        builder.HasIndex(item => new { item.ExceptionId, item.CreatedAtUtc });
        builder.HasOne(item => item.Exception).WithMany(exception => exception.History).HasForeignKey(item => item.ExceptionId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_REH_Exception");
        builder.HasOne(item => item.Actor).WithMany().HasForeignKey(item => item.ActorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CashPositionSessionConfiguration : IEntityTypeConfiguration<CashPositionSession>
{
    public void Configure(EntityTypeBuilder<CashPositionSession> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.PositionType).IsRequired().HasMaxLength(20);
        builder.Property(item => item.Status).IsRequired().HasMaxLength(20);
        builder.Property(item => item.OpenIdempotencyKey).HasMaxLength(64);
        builder.Property(item => item.Version).IsConcurrencyToken();
        builder.HasIndex(item => new { item.BusinessDate, item.Status });
        builder.HasIndex(item => new { item.TellerId, item.BusinessDate, item.Status });
        builder.HasIndex(item => new { item.OpenedBy, item.OpenIdempotencyKey }).IsUnique().HasDatabaseName("IX_CPS_Idem");
        builder.HasOne(item => item.Teller).WithMany().HasForeignKey(item => item.TellerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CashMovementConfiguration : IEntityTypeConfiguration<CashMovement>
{
    public void Configure(EntityTypeBuilder<CashMovement> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Type).IsRequired().HasMaxLength(30);
        builder.Property(item => item.Status).IsRequired().HasMaxLength(30).HasDefaultValue(OperationsConstants.CashMovementApproved);
        builder.Property(item => item.Note).HasMaxLength(500);
        builder.HasIndex(item => new { item.SessionId, item.CreatedAtUtc });
        builder.HasIndex(item => item.Status);
        builder.HasOne(item => item.Session).WithMany().HasForeignKey(item => item.SessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.DestinationSession).WithMany().HasForeignKey(item => item.DestinationSessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Transaction).WithMany().HasForeignKey(item => item.TransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.CorrectionTransaction).WithMany().HasForeignKey(item => item.CorrectionTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Actor).WithMany().HasForeignKey(item => item.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Approver).WithMany().HasForeignKey(item => item.ApprovedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CashCountConfiguration : IEntityTypeConfiguration<CashCount>
{
    public void Configure(EntityTypeBuilder<CashCount> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Notes).HasMaxLength(2000);
        builder.Property(item => item.IdempotencyKey).HasMaxLength(64);
        builder.HasIndex(item => new { item.SessionId, item.CountedAtUtc });
        builder.HasIndex(item => new { item.CountedBy, item.IdempotencyKey }).IsUnique().HasDatabaseName("IX_CC_Idem");
        builder.HasOne(item => item.Session).WithMany().HasForeignKey(item => item.SessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.CountedByUser).WithMany().HasForeignKey(item => item.CountedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CashHandoffConfiguration : IEntityTypeConfiguration<CashHandoff>
{
    public void Configure(EntityTypeBuilder<CashHandoff> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Status).IsRequired().HasMaxLength(30);
        builder.Property(item => item.Version).IsConcurrencyToken();
        builder.HasIndex(item => item.CashCountId).IsUnique().HasDatabaseName("IX_CH_Count");
        builder.HasIndex(item => new { item.BusinessDate, item.Status }).HasDatabaseName("IX_CH_BD_ST");
        builder.HasIndex(item => new { item.RecipientId, item.Status }).HasDatabaseName("IX_CH_R_ST");
        builder.HasOne(item => item.Session).WithMany().HasForeignKey(item => item.SessionId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_CH_Sess");
        builder.HasOne(item => item.CashCount).WithMany().HasForeignKey(item => item.CashCountId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_CH_Count");
        builder.HasOne(item => item.Sender).WithMany().HasForeignKey(item => item.SenderId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_CH_Send");
        builder.HasOne(item => item.Recipient).WithMany().HasForeignKey(item => item.RecipientId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_CH_Recv");
    }
}

public sealed class CashHandoffHistoryConfiguration : IEntityTypeConfiguration<CashHandoffHistory>
{
    public void Configure(EntityTypeBuilder<CashHandoffHistory> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.OldStatus).IsRequired().HasMaxLength(30);
        builder.Property(item => item.NewStatus).IsRequired().HasMaxLength(30);
        builder.Property(item => item.Note).HasMaxLength(500);
        builder.HasIndex(item => new { item.CashHandoffId, item.CreatedAtUtc }).HasDatabaseName("IX_CHH_Hist");
        builder.HasOne(item => item.CashHandoff).WithMany().HasForeignKey(item => item.CashHandoffId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_CHH_Handoff");
        builder.HasOne(item => item.Actor).WithMany().HasForeignKey(item => item.ActorId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_CHH_Actor");
    }
}

public sealed class EndOfDayRunConfiguration : IEntityTypeConfiguration<EndOfDayRun>
{
    public void Configure(EntityTypeBuilder<EndOfDayRun> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Status).IsRequired().HasMaxLength(30);
        builder.Property(item => item.StageSummaryJson).IsRequired().HasColumnType("json");
        builder.HasIndex(item => new { item.BusinessDate, item.PreparedAtUtc });
        builder.HasOne<bams.server.Models.Security.User>().WithMany().HasForeignKey(item => item.PreparedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<bams.server.Models.Security.User>().WithMany().HasForeignKey(item => item.ApprovedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<bams.server.Models.Security.User>().WithMany().HasForeignKey(item => item.ClosedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
