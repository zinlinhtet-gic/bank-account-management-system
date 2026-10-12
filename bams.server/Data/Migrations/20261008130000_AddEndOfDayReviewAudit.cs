using bams.server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bams.server.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261008130000_AddEndOfDayReviewAudit")]
public partial class AddEndOfDayReviewAudit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "ReviewedBy",
            table: "EndOfDayRuns",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ReviewedAtUtc",
            table: "EndOfDayRuns",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_EndOfDayRuns_ReviewedBy",
            table: "EndOfDayRuns",
            column: "ReviewedBy");

        migrationBuilder.AddForeignKey(
            name: "FK_EndOfDayRuns_Users_ReviewedBy",
            table: "EndOfDayRuns",
            column: "ReviewedBy",
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_EndOfDayRuns_Users_ReviewedBy",
            table: "EndOfDayRuns");

        migrationBuilder.DropIndex(
            name: "IX_EndOfDayRuns_ReviewedBy",
            table: "EndOfDayRuns");

        migrationBuilder.DropColumn(
            name: "ReviewedBy",
            table: "EndOfDayRuns");

        migrationBuilder.DropColumn(
            name: "ReviewedAtUtc",
            table: "EndOfDayRuns");
    }
}
