using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bams.server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionSafeguards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Transactions",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FailedPickupAttempts",
                table: "NrcCashTransferDetails",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_IdempotencyKey",
                table: "Transactions",
                column: "IdempotencyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_IdempotencyKey",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "FailedPickupAttempts",
                table: "NrcCashTransferDetails");
        }
    }
}
