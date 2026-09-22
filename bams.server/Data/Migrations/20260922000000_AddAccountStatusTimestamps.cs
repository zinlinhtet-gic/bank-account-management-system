using System;
using bams.server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bams.server.Data.Migrations;

/// <inheritdoc />
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260922000000_AddAccountStatusTimestamps")]
public partial class AddAccountStatusTimestamps : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "ActiveAt",
            table: "Accounts",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "FrozenAt",
            table: "Accounts",
            type: "datetime(6)",
            nullable: true);

        // Existing active accounts predate this column, so their opening time is the best known activation time.
        migrationBuilder.Sql(
            "UPDATE `Accounts` SET `ActiveAt` = `OpenedAt` WHERE `Status` = 1 AND `ActiveAt` IS NULL;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ActiveAt",
            table: "Accounts");

        migrationBuilder.DropColumn(
            name: "FrozenAt",
            table: "Accounts");
    }
}
