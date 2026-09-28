using bams.server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bams.server.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260928140000_UseAccountTypeCategory")]
public sealed class UseAccountTypeCategory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "UPDATE `AccountTypes` SET `Category` = CASE " +
            "WHEN UPPER(COALESCE(`Category`, '')) IN ('CURRENT') THEN 'CURRENT' " +
            "WHEN UPPER(COALESCE(`Category`, '')) IN ('SAVING', 'SAVINGS') THEN 'SAVING' " +
            "WHEN UPPER(COALESCE(`Category`, '')) IN ('CALL') THEN 'CALL' " +
            "WHEN UPPER(COALESCE(`Category`, '')) IN ('FIXED', 'DEPOSIT') OR `IsFixedDeposit` = TRUE THEN 'FIXED' " +
            "WHEN UPPER(`Code`) LIKE '%CURRENT%' THEN 'CURRENT' " +
            "WHEN UPPER(`Code`) LIKE '%SAV%' THEN 'SAVING' " +
            "WHEN UPPER(`Code`) LIKE '%CALL%' THEN 'CALL' " +
            "ELSE 'FIXED' END");

        migrationBuilder.AlterColumn<string>(
            name: "Category",
            table: "AccountTypes",
            type: "varchar(20)",
            maxLength: 20,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(40)",
            oldMaxLength: 40,
            oldNullable: true);

        migrationBuilder.DropColumn(name: "IsFixedDeposit", table: "AccountTypes");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsFixedDeposit",
            table: "AccountTypes",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql(
            "UPDATE `AccountTypes` SET `IsFixedDeposit` = (`Category` = 'FIXED'), `Category` = CASE `Category` " +
            "WHEN 'CURRENT' THEN 'Current' WHEN 'SAVING' THEN 'Saving' " +
            "WHEN 'FIXED' THEN 'Deposit' WHEN 'CALL' THEN 'Call' ELSE 'Deposit' END");

        migrationBuilder.AlterColumn<string>(
            name: "Category",
            table: "AccountTypes",
            type: "varchar(40)",
            maxLength: 40,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(20)",
            oldMaxLength: 20);
    }
}
