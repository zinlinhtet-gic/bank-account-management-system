using bams.server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace bams.server.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261002120000_AddScheduledJobFailureAlerts")]
public sealed class AddScheduledJobFailureAlerts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // These single-column indexes were replaced by date-aware composites in the prior lifecycle migration.
        migrationBuilder.DropIndex("IX_TransactionEntries_CustomerAccountId", "TransactionEntries");
        migrationBuilder.DropIndex("IX_CashPositionSessions_TellerId", "CashPositionSessions");
        migrationBuilder.DropIndex("IX_AccountTransactions_AccountId", "AccountTransactions");

        migrationBuilder.AddColumn<long>(name: "RetryRequestId", table: "ScheduledJobExecutions", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<int>(name: "FailureCode", table: "ScheduledJobExecutions", type: "int", nullable: true);
        migrationBuilder.AddColumn<string>(name: "FailureSummary", table: "ScheduledJobExecutions", type: "varchar(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "FinalFailureAtUtc", table: "ScheduledJobExecutions", type: "datetime(6)", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "FailureRetryRequestedAtUtc", table: "ScheduledJobExecutions", type: "datetime(6)", nullable: true);
        migrationBuilder.AddColumn<long>(name: "FailureRetryRequestedBy", table: "ScheduledJobExecutions", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "FailureResolvedAtUtc", table: "ScheduledJobExecutions", type: "datetime(6)", nullable: true);

        migrationBuilder.CreateTable(
            name: "ScheduledJobRetryRequests",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                ScheduledJobId = table.Column<long>(type: "bigint", nullable: false),
                FailedExecutionId = table.Column<long>(type: "bigint", nullable: false),
                ScheduledForUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                ResumeNextRunAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RequestedBy = table.Column<long>(type: "bigint", nullable: false),
                RequestedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                Status = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false),
                CompletedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ScheduledJobRetryRequests", row => row.Id);
                table.ForeignKey("FK_SJRetry_Job", row => row.ScheduledJobId, "ScheduledJobs", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_SJRetry_FailedExec", row => row.FailedExecutionId, "ScheduledJobExecutions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_SJRetry_User", row => row.RequestedBy, "Users", "Id", onDelete: ReferentialAction.Restrict);
            })
            .Annotation("MySQL:Charset", "utf8mb4");

        migrationBuilder.CreateIndex("IX_SJExec_FinalFailure", "ScheduledJobExecutions", new[] { "FinalFailureAtUtc", "FailureResolvedAtUtc" });
        migrationBuilder.CreateIndex("IX_SJExec_RetryReqId", "ScheduledJobExecutions", "RetryRequestId");
        migrationBuilder.CreateIndex("IX_SJExec_RetryUserId", "ScheduledJobExecutions", "FailureRetryRequestedBy");
        migrationBuilder.CreateIndex("IX_SJRetry_JobStatusDate", "ScheduledJobRetryRequests", new[] { "ScheduledJobId", "Status", "ScheduledForUtc" });
        migrationBuilder.CreateIndex("IX_SJRetry_FailedExec", "ScheduledJobRetryRequests", "FailedExecutionId", unique: true);
        migrationBuilder.CreateIndex("IX_SJRetry_RequestedBy", "ScheduledJobRetryRequests", "RequestedBy");

        migrationBuilder.AddForeignKey(name: "FK_SJExec_RetryReq", table: "ScheduledJobExecutions", column: "RetryRequestId",
            principalTable: "ScheduledJobRetryRequests", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_SJExec_RetryUser", table: "ScheduledJobExecutions", column: "FailureRetryRequestedBy",
            principalTable: "Users", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_SJExec_RetryReq", "ScheduledJobExecutions");
        migrationBuilder.DropForeignKey("FK_SJExec_RetryUser", "ScheduledJobExecutions");
        migrationBuilder.DropTable("ScheduledJobRetryRequests");
        migrationBuilder.DropIndex("IX_SJExec_FinalFailure", "ScheduledJobExecutions");
        migrationBuilder.DropIndex("IX_SJExec_RetryReqId", "ScheduledJobExecutions");
        migrationBuilder.DropIndex("IX_SJExec_RetryUserId", "ScheduledJobExecutions");
        migrationBuilder.DropColumn("RetryRequestId", "ScheduledJobExecutions");
        migrationBuilder.DropColumn("FailureCode", "ScheduledJobExecutions");
        migrationBuilder.DropColumn("FailureSummary", "ScheduledJobExecutions");
        migrationBuilder.DropColumn("FinalFailureAtUtc", "ScheduledJobExecutions");
        migrationBuilder.DropColumn("FailureRetryRequestedAtUtc", "ScheduledJobExecutions");
        migrationBuilder.DropColumn("FailureRetryRequestedBy", "ScheduledJobExecutions");
        migrationBuilder.DropColumn("FailureResolvedAtUtc", "ScheduledJobExecutions");
        migrationBuilder.CreateIndex("IX_TransactionEntries_CustomerAccountId", "TransactionEntries", "CustomerAccountId");
        migrationBuilder.CreateIndex("IX_CashPositionSessions_TellerId", "CashPositionSessions", "TellerId");
        migrationBuilder.CreateIndex("IX_AccountTransactions_AccountId", "AccountTransactions", "AccountId");
    }
}
