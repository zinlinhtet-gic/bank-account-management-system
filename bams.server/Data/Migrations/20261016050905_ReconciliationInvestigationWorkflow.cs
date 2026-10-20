using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace bams.server.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReconciliationInvestigationWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AccountingRootTransactionId",
                table: "Transactions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiscrepancyKind",
                table: "ReconciliationExceptions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewDueDate",
                table: "ReconciliationExceptions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "BusinessDates",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ActiveDateKey",
                table: "BusinessDates",
                type: "int",
                nullable: true,
                computedColumnSql: "CASE WHEN `IsActive` = 1 THEN 1 ELSE NULL END",
                stored: true);

            migrationBuilder.CreateTable(
                name: "CorrectionProposals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ExceptionId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Verification = table.Column<int>(type: "int", nullable: false),
                    ProposerId = table.Column<long>(type: "bigint", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    InputsJson = table.Column<string>(type: "longtext", nullable: false),
                    PreviewJson = table.Column<string>(type: "longtext", nullable: false),
                    SourceHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    ReviewerId = table.Column<long>(type: "bigint", nullable: true),
                    ReviewNote = table.Column<string>(type: "varchar(3000)", maxLength: 3000, nullable: true),
                    SoleManagerApproval = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ExecutionTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    VerificationNote = table.Column<string>(type: "longtext", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrectionProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrectionProposals_ReconciliationExceptions_ExceptionId",
                        column: x => x.ExceptionId,
                        principalTable: "ReconciliationExceptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DailySummaryRevisions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    BusinessDate = table.Column<DateTime>(type: "date", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    IsFinal = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsStale = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LinesJson = table.Column<string>(type: "longtext", nullable: false),
                    ExceptionsJson = table.Column<string>(type: "longtext", nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailySummaryRevisions", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EffectiveAccountBalances",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    AccountId = table.Column<long>(type: "bigint", nullable: false),
                    AccountTransactionId = table.Column<long>(type: "bigint", nullable: false),
                    ProposalId = table.Column<long>(type: "bigint", nullable: false),
                    BalanceBefore = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CalculatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EffectiveAccountBalances", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            // Myanmar business day: select one eligible open date without reopening finalized dates.
            migrationBuilder.Sql("UPDATE BusinessDates SET IsActive = 1 WHERE Id = (SELECT Id FROM (SELECT Id FROM BusinessDates WHERE Status = 'Open' AND Date <= DATE(DATE_ADD(UTC_TIMESTAMP(), INTERVAL 390 MINUTE)) ORDER BY Date DESC LIMIT 1) active_date)");
            // Preserve previously generated summaries as revision one, retaining their original values.
            migrationBuilder.Sql(@"INSERT INTO DailySummaryRevisions (BusinessDate, Revision, IsFinal, IsStale, LinesJson, ExceptionsJson, GeneratedAtUtc)
                SELECT d.SummaryDate, 1, MAX(CASE WHEN b.Status = 'Closed' THEN 1 ELSE 0 END), 0,
                    JSON_ARRAYAGG(JSON_OBJECT('GlAccountId', d.GlAccountId, 'OpeningBalance', d.OpeningBalance,
                    'TotalDebit', d.TotalDebit, 'TotalCredit', d.TotalCredit, 'ClosingBalance', d.ClosingBalance)), '[]', MAX(d.GeneratedAt)
                FROM DailySummaries d LEFT JOIN BusinessDates b ON b.Date = d.SummaryDate GROUP BY d.SummaryDate");
            migrationBuilder.Sql(@"INSERT INTO ReconciliationExceptionHistories (ExceptionId, OldStatus, NewStatus, Note, ActorId, CreatedAtUtc)
                SELECT Id, Status, Status, 'Legacy pending reversal requires a new discrepancy proposal and preview.',
                    COALESCE(CorrectionRequestedBy, CreatedBy), UTC_TIMESTAMP() FROM ReconciliationExceptions WHERE CorrectionRequestStatus = 'Pending'");
            migrationBuilder.Sql("UPDATE ReconciliationExceptions SET CorrectionRequestStatus = 'Superseded' WHERE CorrectionRequestStatus = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessDates_ActiveDateKey",
                table: "BusinessDates",
                column: "ActiveDateKey",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_BusinessDates_ActiveOpen",
                table: "BusinessDates",
                sql: "`IsActive` = 0 OR `Status` = 'Open'");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectionProposals_ExceptionId_Revision",
                table: "CorrectionProposals",
                columns: new[] { "ExceptionId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorrectionProposals_Status_Verification",
                table: "CorrectionProposals",
                columns: new[] { "Status", "Verification" });

            migrationBuilder.CreateIndex(
                name: "IX_DailySummaryRevisions_BusinessDate_Revision",
                table: "DailySummaryRevisions",
                columns: new[] { "BusinessDate", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EffectiveAccountBalances_AccountTransactionId_Id",
                table: "EffectiveAccountBalances",
                columns: new[] { "AccountTransactionId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_EffectiveAccountBalances_ProposalId_AccountTransactionId",
                table: "EffectiveAccountBalances",
                columns: new[] { "ProposalId", "AccountTransactionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CorrectionProposals");

            migrationBuilder.DropTable(
                name: "DailySummaryRevisions");

            migrationBuilder.DropTable(
                name: "EffectiveAccountBalances");

            migrationBuilder.DropIndex(
                name: "IX_BusinessDates_ActiveDateKey",
                table: "BusinessDates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BusinessDates_ActiveOpen",
                table: "BusinessDates");

            migrationBuilder.DropColumn(
                name: "ActiveDateKey",
                table: "BusinessDates");

            migrationBuilder.DropColumn(
                name: "AccountingRootTransactionId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "DiscrepancyKind",
                table: "ReconciliationExceptions");

            migrationBuilder.DropColumn(
                name: "ReviewDueDate",
                table: "ReconciliationExceptions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "BusinessDates");
        }
    }
}
