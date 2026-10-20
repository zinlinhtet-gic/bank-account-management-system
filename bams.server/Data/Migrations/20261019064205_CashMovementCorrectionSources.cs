using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bams.server.Data.Migrations
{
    /// <inheritdoc />
    public partial class CashMovementCorrectionSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CashCorrectionProposalId",
                table: "CashMovements",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CorrectsMovementId",
                table: "CashMovements",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_CashCorrectionProposalId",
                table: "CashMovements",
                column: "CashCorrectionProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_CorrectsMovementId",
                table: "CashMovements",
                column: "CorrectsMovementId");

            migrationBuilder.AddForeignKey(
                name: "FK_CashMovements_CashCorrectionProposals_CashCorrectionProposal~",
                table: "CashMovements",
                column: "CashCorrectionProposalId",
                principalTable: "CashCorrectionProposals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CashMovements_CashMovements_CorrectsMovementId",
                table: "CashMovements",
                column: "CorrectsMovementId",
                principalTable: "CashMovements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashMovements_CashCorrectionProposals_CashCorrectionProposal~",
                table: "CashMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_CashMovements_CashMovements_CorrectsMovementId",
                table: "CashMovements");

            migrationBuilder.DropIndex(
                name: "IX_CashMovements_CashCorrectionProposalId",
                table: "CashMovements");

            migrationBuilder.DropIndex(
                name: "IX_CashMovements_CorrectsMovementId",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "CashCorrectionProposalId",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "CorrectsMovementId",
                table: "CashMovements");
        }
    }
}
