using bams.server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bams.server.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260924000000_AddAccountOptimisticConcurrency")]
public partial class AddAccountOptimisticConcurrency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "Version",
            table: "Accounts",
            type: "bigint",
            nullable: false,
            defaultValue: 1L);

        migrationBuilder.AddColumn<long>(
            name: "Version",
            table: "AccountHolders",
            type: "bigint",
            nullable: false,
            defaultValue: 1L);

        migrationBuilder.AddColumn<long>(
            name: "Version",
            table: "FixedDeposits",
            type: "bigint",
            nullable: false,
            defaultValue: 1L);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Version", table: "Accounts");
        migrationBuilder.DropColumn(name: "Version", table: "AccountHolders");
        migrationBuilder.DropColumn(name: "Version", table: "FixedDeposits");
    }
}
