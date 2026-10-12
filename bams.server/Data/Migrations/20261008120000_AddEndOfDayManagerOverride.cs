using bams.server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bams.server.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261008120000_AddEndOfDayManagerOverride")]
public sealed class AddEndOfDayManagerOverride : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "OverrideReason", table: "EndOfDayRuns", type: "longtext", nullable: true);
        migrationBuilder.AddColumn<string>(name: "OverrideRequiredUserIdsJson", table: "EndOfDayRuns", type: "longtext", nullable: true);
        migrationBuilder.AddColumn<string>(name: "OverrideApprovedUserIdsJson", table: "EndOfDayRuns", type: "longtext", nullable: true);
        migrationBuilder.AddColumn<string>(name: "OverrideApprovalAuditJson", table: "EndOfDayRuns", type: "longtext", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "OverrideReason", table: "EndOfDayRuns");
        migrationBuilder.DropColumn(name: "OverrideRequiredUserIdsJson", table: "EndOfDayRuns");
        migrationBuilder.DropColumn(name: "OverrideApprovedUserIdsJson", table: "EndOfDayRuns");
        migrationBuilder.DropColumn(name: "OverrideApprovalAuditJson", table: "EndOfDayRuns");
    }
}
