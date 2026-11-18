using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace bams.server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AuditorCashReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditorCashReturns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    BusinessDate = table.Column<DateTime>(type: "date", nullable: false),
                    AuditorId = table.Column<long>(type: "bigint", nullable: false),
                    ManagerId = table.Column<long>(type: "bigint", nullable: false),
                    HandoffIdsJson = table.Column<string>(type: "json", nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    DecisionNote = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditorCashReturns", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AuditorCashReturns_BusinessDate_Status",
                table: "AuditorCashReturns",
                columns: new[] { "BusinessDate", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditorCashReturns");
        }
    }
}
