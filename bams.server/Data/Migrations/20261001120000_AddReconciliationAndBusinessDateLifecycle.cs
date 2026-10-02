using bams.server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace bams.server.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261001120000_AddReconciliationAndBusinessDateLifecycle")]
public sealed class AddReconciliationAndBusinessDateLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(name: "BusinessDate", table: "Transactions", type: "date", nullable: true);
        migrationBuilder.Sql("UPDATE Transactions t SET BusinessDate = COALESCE((SELECT MIN(e.PostingDate) FROM TransactionEntries e WHERE e.TransactionId = t.Id), DATE(DATE_ADD(t.TransactionAt, INTERVAL 390 MINUTE))) WHERE t.BusinessDate IS NULL");
        migrationBuilder.CreateIndex("IX_Transactions_BusinessDate_TransactionStatus", "Transactions", new[] { "BusinessDate", "TransactionStatus" });

        migrationBuilder.CreateTable(name: "BusinessDates", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
            Date = table.Column<DateTime>(type: "date", nullable: false),
            Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
            OpenedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
            OpenedBy = table.Column<long>(type: "bigint", nullable: false),
            ClosedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
            ClosedBy = table.Column<long>(type: "bigint", nullable: true),
            Version = table.Column<long>(type: "bigint", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_BusinessDates", row => row.Id)).Annotation("MySQL:Charset", "utf8mb4");

        migrationBuilder.CreateTable(name: "AccountReconciliationRuns", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
            FromDate = table.Column<DateTime>(type: "date", nullable: false), ToDate = table.Column<DateTime>(type: "date", nullable: false),
            AccountId = table.Column<long>(type: "bigint", nullable: true), ScheduledJobExecutionId = table.Column<long>(type: "bigint", nullable: true),
            Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
            PerformedBy = table.Column<long>(type: "bigint", nullable: false), PerformedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_AccountReconciliationRuns", row => row.Id);
            table.ForeignKey("FK_AccountReconciliationRuns_Accounts_AccountId", row => row.AccountId, "Accounts", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_ARR_ScheduledExec", row => row.ScheduledJobExecutionId,
                "ScheduledJobExecutions", "Id", onDelete: ReferentialAction.Restrict);
        }).Annotation("MySQL:Charset", "utf8mb4");

        migrationBuilder.CreateTable(name: "CashPositionSessions", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
            BranchId = table.Column<long>(type: "bigint", nullable: false), PositionType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
            TellerId = table.Column<long>(type: "bigint", nullable: true), BusinessDate = table.Column<DateTime>(type: "date", nullable: false),
            OpeningCash = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
            ExpectedClosingCash = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
            Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false), OpenedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
            OpenedBy = table.Column<long>(type: "bigint", nullable: false), ClosedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
            ClosedBy = table.Column<long>(type: "bigint", nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_CashPositionSessions", row => row.Id);
            table.ForeignKey("FK_CashPositionSessions_Branches_BranchId", row => row.BranchId, "Branches", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_CashPositionSessions_Users_TellerId", row => row.TellerId, "Users", "Id", onDelete: ReferentialAction.Restrict);
        }).Annotation("MySQL:Charset", "utf8mb4");

        migrationBuilder.CreateTable(name: "ReconciliationExceptions", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
            Type = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false), Source = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
            BusinessDate = table.Column<DateTime>(type: "date", nullable: false), BranchId = table.Column<long>(type: "bigint", nullable: true), AccountId = table.Column<long>(type: "bigint", nullable: true),
            PositionSessionId = table.Column<long>(type: "bigint", nullable: true),
            ExpectedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
            ActualAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
            Difference = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
            Severity = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false), Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
            AssignedTo = table.Column<long>(type: "bigint", nullable: true), RelatedTransactionId = table.Column<long>(type: "bigint", nullable: true),
            CorrectionTransactionId = table.Column<long>(type: "bigint", nullable: true), Notes = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
            CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false), CreatedBy = table.Column<long>(type: "bigint", nullable: false),
            UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_ReconciliationExceptions", row => row.Id);
            table.ForeignKey("FK_ReconciliationExceptions_Branches_BranchId", row => row.BranchId, "Branches", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_ReconciliationExceptions_Accounts_AccountId", row => row.AccountId, "Accounts", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_RE_PositionSession", row => row.PositionSessionId, "CashPositionSessions", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_ReconciliationExceptions_Users_AssignedTo", row => row.AssignedTo, "Users", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_ReconciliationExceptions_Users_CreatedBy", row => row.CreatedBy, "Users", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_ReconciliationExceptions_Transactions_RelatedTransactionId", row => row.RelatedTransactionId, "Transactions", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_ReconciliationExceptions_Transactions_CorrectionTransactionId", row => row.CorrectionTransactionId, "Transactions", "Id", onDelete: ReferentialAction.Restrict);
        }).Annotation("MySQL:Charset", "utf8mb4");

        migrationBuilder.CreateTable(name: "EndOfDayRuns", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
            BusinessDate = table.Column<DateTime>(type: "date", nullable: false), Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
            StageSummaryJson = table.Column<string>(type: "json", nullable: false), PreparedBy = table.Column<long>(type: "bigint", nullable: false),
            PreparedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false), ApprovedBy = table.Column<long>(type: "bigint", nullable: true),
            ApprovedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true), ClosedBy = table.Column<long>(type: "bigint", nullable: true),
            ClosedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_EndOfDayRuns", row => row.Id);
            table.ForeignKey("FK_EndOfDayRuns_Users_PreparedBy", row => row.PreparedBy, "Users", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_EndOfDayRuns_Users_ApprovedBy", row => row.ApprovedBy, "Users", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_EndOfDayRuns_Users_ClosedBy", row => row.ClosedBy, "Users", "Id", onDelete: ReferentialAction.Restrict);
        }).Annotation("MySQL:Charset", "utf8mb4");

        migrationBuilder.CreateTable(name: "AccountReconciliationResults", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
            RunId = table.Column<long>(type: "bigint", nullable: false), AccountId = table.Column<long>(type: "bigint", nullable: false),
            BusinessDate = table.Column<DateTime>(type: "date", nullable: false),
            OperationalBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
            LedgerBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
            Difference = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false), Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_AccountReconciliationResults", row => row.Id);
            table.ForeignKey("FK_AccountReconciliationResults_AccountReconciliationRuns_RunId", row => row.RunId, "AccountReconciliationRuns", "Id", onDelete: ReferentialAction.Cascade);
            table.ForeignKey("FK_AccountReconciliationResults_Accounts_AccountId", row => row.AccountId, "Accounts", "Id", onDelete: ReferentialAction.Restrict);
        }).Annotation("MySQL:Charset", "utf8mb4");

        migrationBuilder.CreateTable(name: "ReconciliationExceptionHistories", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
            ExceptionId = table.Column<long>(type: "bigint", nullable: false), OldStatus = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
            NewStatus = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false), Note = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
            ActorId = table.Column<long>(type: "bigint", nullable: false), CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_ReconciliationExceptionHistories", row => row.Id);
            table.ForeignKey("FK_REH_Exception", row => row.ExceptionId, "ReconciliationExceptions", "Id", onDelete: ReferentialAction.Cascade);
            table.ForeignKey("FK_ReconciliationExceptionHistories_Users_ActorId", row => row.ActorId, "Users", "Id", onDelete: ReferentialAction.Restrict);
        }).Annotation("MySQL:Charset", "utf8mb4");

        migrationBuilder.CreateTable(name: "CashMovements", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
            SessionId = table.Column<long>(type: "bigint", nullable: false), DestinationSessionId = table.Column<long>(type: "bigint", nullable: true),
            Type = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false), Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false, defaultValue: "Approved"),
            Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
            TransactionId = table.Column<long>(type: "bigint", nullable: true), CorrectionTransactionId = table.Column<long>(type: "bigint", nullable: true),
            ActorId = table.Column<long>(type: "bigint", nullable: false), ApprovedBy = table.Column<long>(type: "bigint", nullable: true),
            ApprovedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true), CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
            Note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_CashMovements", row => row.Id);
            table.ForeignKey("FK_CashMovements_CashPositionSessions_SessionId", row => row.SessionId, "CashPositionSessions", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_CashMovements_CashPositionSessions_DestinationSessionId", row => row.DestinationSessionId, "CashPositionSessions", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_CashMovements_Transactions_TransactionId", row => row.TransactionId, "Transactions", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_CashMovements_Transactions_CorrectionTransactionId", row => row.CorrectionTransactionId, "Transactions", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_CashMovements_Users_ActorId", row => row.ActorId, "Users", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_CashMovements_Users_ApprovedBy", row => row.ApprovedBy, "Users", "Id", onDelete: ReferentialAction.Restrict);
        }).Annotation("MySQL:Charset", "utf8mb4");

        migrationBuilder.CreateTable(name: "CashCounts", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
            SessionId = table.Column<long>(type: "bigint", nullable: false), ExpectedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
            ActualAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false), Difference = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
            Notes = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true), CountedBy = table.Column<long>(type: "bigint", nullable: false), CountedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_CashCounts", row => row.Id);
            table.ForeignKey("FK_CashCounts_CashPositionSessions_SessionId", row => row.SessionId, "CashPositionSessions", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_CashCounts_Users_CountedBy", row => row.CountedBy, "Users", "Id", onDelete: ReferentialAction.Restrict);
        }).Annotation("MySQL:Charset", "utf8mb4");

        migrationBuilder.CreateIndex("IX_BusinessDates_Date", "BusinessDates", "Date", unique: true);
        migrationBuilder.CreateIndex("IX_BusinessDates_Status", "BusinessDates", "Status");
        migrationBuilder.CreateIndex("IX_AccountReconciliationRuns_AccountId", "AccountReconciliationRuns", "AccountId");
        migrationBuilder.CreateIndex("IX_AccountReconciliationRuns_ScheduledJobExecutionId", "AccountReconciliationRuns", "ScheduledJobExecutionId", unique: true);
        migrationBuilder.CreateIndex("IX_AccountReconciliationRuns_FromDate_ToDate_PerformedAtUtc", "AccountReconciliationRuns", new[] { "FromDate", "ToDate", "PerformedAtUtc" });
        migrationBuilder.CreateIndex("IX_AccountReconciliationResults_AccountId_BusinessDate", "AccountReconciliationResults", new[] { "AccountId", "BusinessDate" });
        migrationBuilder.CreateIndex("IX_AccountReconciliationResults_RunId", "AccountReconciliationResults", "RunId");
        migrationBuilder.CreateIndex("IX_ReconciliationExceptions_BusinessDate_Status_Severity", "ReconciliationExceptions", new[] { "BusinessDate", "Status", "Severity" });
        migrationBuilder.CreateIndex("IX_ReconciliationExceptions_AccountId_BusinessDate_Status", "ReconciliationExceptions", new[] { "AccountId", "BusinessDate", "Status" });
        migrationBuilder.CreateIndex("IX_ReconciliationExceptions_PositionSessionId", "ReconciliationExceptions", "PositionSessionId");
        migrationBuilder.CreateIndex("IX_ReconciliationExceptions_CreatedBy", "ReconciliationExceptions", "CreatedBy");
        migrationBuilder.CreateIndex("IX_ReconciliationExceptions_BranchId", "ReconciliationExceptions", "BranchId");
        migrationBuilder.CreateIndex("IX_ReconciliationExceptions_AssignedTo", "ReconciliationExceptions", "AssignedTo");
        migrationBuilder.CreateIndex("IX_ReconciliationExceptions_RelatedTransactionId", "ReconciliationExceptions", "RelatedTransactionId");
        migrationBuilder.CreateIndex("IX_ReconciliationExceptions_CorrectionTransactionId", "ReconciliationExceptions", "CorrectionTransactionId");
        migrationBuilder.CreateIndex("IX_CashPositionSessions_BranchId_BusinessDate_Status", "CashPositionSessions", new[] { "BranchId", "BusinessDate", "Status" });
        migrationBuilder.CreateIndex("IX_CashPositionSessions_BusinessDate_Status", "CashPositionSessions", new[] { "BusinessDate", "Status" });
        migrationBuilder.CreateIndex("IX_CashPositionSessions_TellerId_BusinessDate_Status", "CashPositionSessions", new[] { "TellerId", "BusinessDate", "Status" });
        migrationBuilder.CreateIndex("IX_CashPositionSessions_TellerId", "CashPositionSessions", "TellerId");
        migrationBuilder.CreateIndex("IX_ReconciliationExceptionHistories_ExceptionId_CreatedAtUtc", "ReconciliationExceptionHistories", new[] { "ExceptionId", "CreatedAtUtc" });
        migrationBuilder.CreateIndex("IX_ReconciliationExceptionHistories_ActorId", "ReconciliationExceptionHistories", "ActorId");
        migrationBuilder.CreateIndex("IX_CashMovements_SessionId_CreatedAtUtc", "CashMovements", new[] { "SessionId", "CreatedAtUtc" });
        migrationBuilder.CreateIndex("IX_CashMovements_DestinationSessionId", "CashMovements", "DestinationSessionId");
        migrationBuilder.CreateIndex("IX_CashMovements_TransactionId", "CashMovements", "TransactionId");
        migrationBuilder.CreateIndex("IX_CashMovements_CorrectionTransactionId", "CashMovements", "CorrectionTransactionId");
        migrationBuilder.CreateIndex("IX_CashMovements_ActorId", "CashMovements", "ActorId");
        migrationBuilder.CreateIndex("IX_CashMovements_ApprovedBy", "CashMovements", "ApprovedBy");
        migrationBuilder.CreateIndex("IX_CashMovements_Status", "CashMovements", "Status");
        migrationBuilder.CreateIndex("IX_CashCounts_SessionId_CountedAtUtc", "CashCounts", new[] { "SessionId", "CountedAtUtc" });
        migrationBuilder.CreateIndex("IX_CashCounts_CountedBy", "CashCounts", "CountedBy");
        migrationBuilder.CreateIndex("IX_EndOfDayRuns_BusinessDate_PreparedAtUtc", "EndOfDayRuns", new[] { "BusinessDate", "PreparedAtUtc" });
        migrationBuilder.CreateIndex("IX_EndOfDayRuns_PreparedBy", "EndOfDayRuns", "PreparedBy");
        migrationBuilder.CreateIndex("IX_EndOfDayRuns_ApprovedBy", "EndOfDayRuns", "ApprovedBy");
        migrationBuilder.CreateIndex("IX_EndOfDayRuns_ClosedBy", "EndOfDayRuns", "ClosedBy");
        migrationBuilder.CreateIndex("IX_AccountTransactions_AccountId_PostingDate_Id", "AccountTransactions", new[] { "AccountId", "PostingDate", "Id" });
        migrationBuilder.CreateIndex("IX_TransactionEntries_CustomerAccountId_PostingDate_GlAccountId", "TransactionEntries", new[] { "CustomerAccountId", "PostingDate", "GlAccountId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_Transactions_BusinessDate_TransactionStatus", "Transactions");
        migrationBuilder.DropColumn(name: "BusinessDate", table: "Transactions");
        migrationBuilder.DropIndex("IX_AccountTransactions_AccountId_PostingDate_Id", "AccountTransactions");
        migrationBuilder.DropIndex("IX_TransactionEntries_CustomerAccountId_PostingDate_GlAccountId", "TransactionEntries");
        migrationBuilder.DropTable("CashCounts");
        migrationBuilder.DropTable("CashMovements");
        migrationBuilder.DropTable("ReconciliationExceptionHistories");
        migrationBuilder.DropTable("AccountReconciliationResults");
        migrationBuilder.DropTable("EndOfDayRuns");
        migrationBuilder.DropTable("ReconciliationExceptions");
        migrationBuilder.DropTable("CashPositionSessions");
        migrationBuilder.DropTable("AccountReconciliationRuns");
        migrationBuilder.DropTable("BusinessDates");
    }
}


