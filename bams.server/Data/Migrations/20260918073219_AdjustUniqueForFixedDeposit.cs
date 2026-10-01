using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bams.server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdjustUniqueForFixedDeposit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create the replacement first so AccountId remains indexed for its foreign key.
            migrationBuilder.CreateIndex(
                name: "IX_FixedDeposits_AccountId_StartDate_MaturityDate",
                table: "FixedDeposits",
                columns: new[] { "AccountId", "StartDate", "MaturityDate" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_FixedDeposits_AccountId",
                table: "FixedDeposits");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the original index first so the foreign key stays indexed during rollback.
            migrationBuilder.CreateIndex(
                name: "IX_FixedDeposits_AccountId",
                table: "FixedDeposits",
                column: "AccountId",
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_FixedDeposits_AccountId_StartDate_MaturityDate",
                table: "FixedDeposits");
        }
    }
}
