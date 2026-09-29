using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;
using bams.server.Data;

#nullable disable

namespace bams.server.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260928130000_AddReusableScheduledJobs")]
public sealed class AddReusableScheduledJobs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ScheduledJobs",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                JobKey = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                DisplayName = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false),
                ScheduleType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                IntervalTicks = table.Column<long>(type: "bigint", nullable: true),
                DayOfMonth = table.Column<int>(type: "int", nullable: true),
                LocalTime = table.Column<TimeSpan>(type: "time(6)", nullable: true),
                TimeZoneId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                IsEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                NextRunAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                PendingScheduledAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                LeaseToken = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: true),
                LeaseUntilUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                LastRunAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                Status = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ScheduledJobs", row => row.Id))
            .Annotation("MySQL:Charset", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "ScheduledJobExecutions",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                ScheduledJobId = table.Column<long>(type: "bigint", nullable: false),
                ScheduledForUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                AttemptNumber = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false),
                StartedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                CompletedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                ErrorMessage = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ScheduledJobExecutions", row => row.Id);
                table.ForeignKey(
                    name: "FK_ScheduledJobExecutions_ScheduledJobs_ScheduledJobId",
                    column: row => row.ScheduledJobId,
                    principalTable: "ScheduledJobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySQL:Charset", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_ScheduledJobs_JobKey",
            table: "ScheduledJobs",
            column: "JobKey",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_SJobs_IsEnabled_NextAtUtc_LeaseUtc",
            table: "ScheduledJobs",
            columns: new[] { "IsEnabled", "NextRunAtUtc", "LeaseUntilUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_SJobExecutions_SJId_SUtc_Attempt",
            table: "ScheduledJobExecutions",
            columns: new[] { "ScheduledJobId", "ScheduledForUtc", "AttemptNumber" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ScheduledJobExecutions");
        migrationBuilder.DropTable(name: "ScheduledJobs");
    }
}
