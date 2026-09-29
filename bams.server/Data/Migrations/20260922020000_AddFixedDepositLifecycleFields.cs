using bams.server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bams.server.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260922020000_AddFixedDepositLifecycleFields")]
public partial class AddFixedDepositLifecycleFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "CalculateFromCurrent", table: "FixedDeposits", type: "tinyint(1)", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<long>(name: "RequiredProductId", table: "AccountTypes", type: "bigint", nullable: true);
        migrationBuilder.CreateIndex(name: "IX_AccountTypes_RequiredProductId", table: "AccountTypes", column: "RequiredProductId");
        migrationBuilder.AddForeignKey(name: "FK_AccountTypes_AccountTypes_RequiredProductId", table: "AccountTypes", column: "RequiredProductId", principalTable: "AccountTypes", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.Sql("UPDATE `AccountTypes` SET `RequiredProductId` = 2 WHERE `Code` IN ('NORMAL_DEPOSIT', 'SPECIAL_DEPOSIT', 'HUNDRED_DAYS_DEPOSIT');");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_AccountTypes_AccountTypes_RequiredProductId", table: "AccountTypes");
        migrationBuilder.DropIndex(name: "IX_AccountTypes_RequiredProductId", table: "AccountTypes");
        migrationBuilder.DropColumn(name: "RequiredProductId", table: "AccountTypes");
        migrationBuilder.DropColumn(name: "CalculateFromCurrent", table: "FixedDeposits");
    }
}
