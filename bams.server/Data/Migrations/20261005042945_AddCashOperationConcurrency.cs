using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bams.server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCashOperationConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OpenIdempotencyKey",
                table: "CashPositionSessions",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "CashPositionSessions",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "CashHandoffs",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "CashCounts",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CPS_Idem",
                table: "CashPositionSessions",
                columns: new[] { "OpenedBy", "OpenIdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CC_Idem",
                table: "CashCounts",
                columns: new[] { "CountedBy", "IdempotencyKey" },
                unique: true);

            // Keep the foreign key's CountedBy index until its replacement composite is available.
            migrationBuilder.DropIndex(
                name: "IX_CashCounts_CountedBy",
                table: "CashCounts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CPS_Idem",
                table: "CashPositionSessions");

            // Restore the foreign key's supporting index before removing the composite replacement.
            migrationBuilder.CreateIndex(
                name: "IX_CashCounts_CountedBy",
                table: "CashCounts",
                column: "CountedBy");

            migrationBuilder.DropIndex(
                name: "IX_CC_Idem",
                table: "CashCounts");

            migrationBuilder.DropColumn(
                name: "OpenIdempotencyKey",
                table: "CashPositionSessions");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "CashPositionSessions");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "CashHandoffs");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "CashCounts");

        }
    }
}
