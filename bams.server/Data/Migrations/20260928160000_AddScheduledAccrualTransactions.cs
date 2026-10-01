using bams.server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bams.server.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260928160000_AddScheduledAccrualTransactions")]
public sealed class AddScheduledAccrualTransactions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "AccruedTransactionId",
            table: "InterestAccruals",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "AccruedTransactionId",
            table: "FeeAccruals",
            type: "bigint",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_InterestAccruals_AccruedTransactionId",
            table: "InterestAccruals",
            column: "AccruedTransactionId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_FeeAccruals_AccruedTransactionId",
            table: "FeeAccruals",
            column: "AccruedTransactionId",
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_InterestAccruals_Transactions_AccruedTransactionId",
            table: "InterestAccruals",
            column: "AccruedTransactionId",
            principalTable: "Transactions",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_FeeAccruals_Transactions_AccruedTransactionId",
            table: "FeeAccruals",
            column: "AccruedTransactionId",
            principalTable: "Transactions",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_InterestAccruals_Transactions_AccruedTransactionId",
            table: "InterestAccruals");

        migrationBuilder.DropForeignKey(
            name: "FK_FeeAccruals_Transactions_AccruedTransactionId",
            table: "FeeAccruals");

        migrationBuilder.DropIndex(
            name: "IX_InterestAccruals_AccruedTransactionId",
            table: "InterestAccruals");

        migrationBuilder.DropIndex(
            name: "IX_FeeAccruals_AccruedTransactionId",
            table: "FeeAccruals");

        migrationBuilder.DropColumn(name: "AccruedTransactionId", table: "InterestAccruals");
        migrationBuilder.DropColumn(name: "AccruedTransactionId", table: "FeeAccruals");
    }
}
