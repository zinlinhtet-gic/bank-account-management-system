using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace bams.server.Data.Migrations
{
    /// <inheritdoc />
    public partial class UnifiedCashExpenseReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankExpenseRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    SessionId = table.Column<long>(type: "bigint", nullable: false),
                    ExceptionId = table.Column<long>(type: "bigint", nullable: true),
                    CashProposalId = table.Column<long>(type: "bigint", nullable: true),
                    BusinessDate = table.Column<DateTime>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Payee = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Purpose = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    ReceiptFileName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    ReceiptContentType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    ReceiptData = table.Column<byte[]>(type: "longblob", nullable: true),
                    MissingReceiptReason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    RequestedBy = table.Column<long>(type: "bigint", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ReviewedBy = table.Column<long>(type: "bigint", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ReviewNote = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    TransactionId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankExpenseRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankExpenseRequests_CashPositionSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "CashPositionSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankExpenseRequests_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CashCorrectionProposals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ExceptionId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Verification = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    ProposerId = table.Column<long>(type: "bigint", nullable: false),
                    ReviewerId = table.Column<long>(type: "bigint", nullable: true),
                    InputsJson = table.Column<string>(type: "json", nullable: false),
                    SourceHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    ReviewNote = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    VerificationNote = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    ExecutionTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashCorrectionProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashCorrectionProposals_ReconciliationExceptions_ExceptionId",
                        column: x => x.ExceptionId,
                        principalTable: "ReconciliationExceptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ReconciliationExclusions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ExceptionId = table.Column<long>(type: "bigint", nullable: false),
                    SourceHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    SourceIdsJson = table.Column<string>(type: "json", nullable: false),
                    Reason = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false),
                    ManagerId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationExclusions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReconciliationExclusions_ReconciliationExceptions_ExceptionId",
                        column: x => x.ExceptionId,
                        principalTable: "ReconciliationExceptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ReconciliationExcludedSources",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ExclusionId = table.Column<long>(type: "bigint", nullable: false),
                    SourceType = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    SourceId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationExcludedSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReconciliationExcludedSources_ReconciliationExclusions_Exclu~",
                        column: x => x.ExclusionId,
                        principalTable: "ReconciliationExclusions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_BankExpenseRequests_BusinessDate_Status",
                table: "BankExpenseRequests",
                columns: new[] { "BusinessDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BankExpenseRequests_SessionId",
                table: "BankExpenseRequests",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_BankExpenseRequests_TransactionId",
                table: "BankExpenseRequests",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_CashCorrectionProposals_ExceptionId_Status",
                table: "CashCorrectionProposals",
                columns: new[] { "ExceptionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationExcludedSources_ExclusionId_SourceType_SourceId",
                table: "ReconciliationExcludedSources",
                columns: new[] { "ExclusionId", "SourceType", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationExclusions_ExceptionId",
                table: "ReconciliationExclusions",
                column: "ExceptionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankExpenseRequests");

            migrationBuilder.DropTable(
                name: "CashCorrectionProposals");

            migrationBuilder.DropTable(
                name: "ReconciliationExcludedSources");

            migrationBuilder.DropTable(
                name: "ReconciliationExclusions");
        }
    }
}
