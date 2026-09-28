using bams.server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bams.server.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260928150000_AddScheduledAccrualIdempotency")]
public sealed class AddScheduledAccrualIdempotency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Keep an equivalent index in place while MySQL rebinds the foreign key's index support.
        migrationBuilder.CreateIndex(
            name: "IX_FeeAccruals_PostedTransactionId_Migration",
            table: "FeeAccruals",
            column: "PostedTransactionId");

        migrationBuilder.DropIndex(
            name: "IX_FeeAccruals_PostedTransactionId",
            table: "FeeAccruals");

        migrationBuilder.CreateIndex(
            name: "IX_FeeAccruals_PostedTransactionId",
            table: "FeeAccruals",
            column: "PostedTransactionId");

        migrationBuilder.DropIndex(
            name: "IX_FeeAccruals_PostedTransactionId_Migration",
            table: "FeeAccruals");

        migrationBuilder.CreateIndex(
            name: "IX_FeeAccruals_AccountId_FeeType_PeriodStart_PeriodEnd",
            table: "FeeAccruals",
            columns: new[] { "AccountId", "FeeType", "PeriodStart", "PeriodEnd" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_InterestAccruals_AccountId_PeriodStart_PeriodEnd",
            table: "InterestAccruals",
            columns: new[] { "AccountId", "PeriodStart", "PeriodEnd" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve the foreign key's supporting index while restoring the unique index definition.
        migrationBuilder.CreateIndex(
            name: "IX_FeeAccruals_PostedTransactionId_Migration",
            table: "FeeAccruals",
            column: "PostedTransactionId",
            unique: true);

        migrationBuilder.DropIndex(
            name: "IX_FeeAccruals_AccountId_FeeType_PeriodStart_PeriodEnd",
            table: "FeeAccruals");

        migrationBuilder.DropIndex(
            name: "IX_InterestAccruals_AccountId_PeriodStart_PeriodEnd",
            table: "InterestAccruals");

        migrationBuilder.DropIndex(
            name: "IX_FeeAccruals_PostedTransactionId",
            table: "FeeAccruals");

        migrationBuilder.CreateIndex(
            name: "IX_FeeAccruals_PostedTransactionId",
            table: "FeeAccruals",
            column: "PostedTransactionId",
            unique: true);

        migrationBuilder.DropIndex(
            name: "IX_FeeAccruals_PostedTransactionId_Migration",
            table: "FeeAccruals");
    }
}
