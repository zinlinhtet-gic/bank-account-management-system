using bams.server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bams.server.Data.Migrations;

/// <inheritdoc />
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260922010000_AddFixedDepositAccountTypeFlag")]
public partial class AddFixedDepositAccountTypeFlag : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsFixedDeposit",
            table: "AccountTypes",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: false);

        // Backfill the fixed-deposit classification for the existing seeded deposit products.
        migrationBuilder.Sql(
            "UPDATE `AccountTypes` SET `IsFixedDeposit` = TRUE " +
            "WHERE `Code` IN ('NORMAL_DEPOSIT', 'SPECIAL_DEPOSIT', 'HUNDRED_DAYS_DEPOSIT');");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsFixedDeposit",
            table: "AccountTypes");
    }
}
