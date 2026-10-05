using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bams.server.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveBranchFromCashReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashPositionSessions_Branches_BranchId",
                table: "CashPositionSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_ReconciliationExceptions_Branches_BranchId",
                table: "ReconciliationExceptions");

            migrationBuilder.DropIndex(
                name: "IX_ReconciliationExceptions_BranchId",
                table: "ReconciliationExceptions");

            migrationBuilder.DropIndex(
                name: "IX_CashPositionSessions_BranchId_BusinessDate_Status",
                table: "CashPositionSessions");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "ReconciliationExceptions");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "CashPositionSessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "BranchId",
                table: "ReconciliationExceptions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "BranchId",
                table: "CashPositionSessions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationExceptions_BranchId",
                table: "ReconciliationExceptions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CashPositionSessions_BranchId_BusinessDate_Status",
                table: "CashPositionSessions",
                columns: new[] { "BranchId", "BusinessDate", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_CashPositionSessions_Branches_BranchId",
                table: "CashPositionSessions",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReconciliationExceptions_Branches_BranchId",
                table: "ReconciliationExceptions",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
