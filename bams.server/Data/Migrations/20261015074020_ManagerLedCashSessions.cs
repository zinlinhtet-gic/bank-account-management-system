using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace bams.server.Data.Migrations
{
    /// <inheritdoc />
    public partial class ManagerLedCashSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ActivatedAtUtc",
                table: "CashPositionSessions",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.Sql("UPDATE CashPositionSessions SET ActivatedAtUtc = OpenedAtUtc WHERE PositionType = 'Teller' AND Status = 'Open'");

            migrationBuilder.AddColumn<decimal>(
                name: "ReservedCash",
                table: "CashPositionSessions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledStartAtMyanmar",
                table: "CashPositionSessions",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledEndAtMyanmar",
                table: "CashPositionSessions",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DestinationSessionId",
                table: "CashHandoffs",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CashProposals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    SessionId = table.Column<long>(type: "bigint", nullable: false),
                    VaultSessionId = table.Column<long>(type: "bigint", nullable: false),
                    OfficerId = table.Column<long>(type: "bigint", nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ApprovedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ApprovedBy = table.Column<long>(type: "bigint", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashProposals_CashPositionSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "CashPositionSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashProposals_CashPositionSessions_VaultSessionId",
                        column: x => x.VaultSessionId,
                        principalTable: "CashPositionSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashProposals_Users_ApprovedBy",
                        column: x => x.ApprovedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashProposals_Users_OfficerId",
                        column: x => x.OfficerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CashPositionSessions_BusinessDate_TellerId_Status",
                table: "CashPositionSessions",
                columns: new[] { "BusinessDate", "TellerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CashHandoffs_DestinationSessionId",
                table: "CashHandoffs",
                column: "DestinationSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CashProposals_ApprovedBy",
                table: "CashProposals",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CashProposals_OfficerId",
                table: "CashProposals",
                column: "OfficerId");

            migrationBuilder.CreateIndex(
                name: "IX_CashProposals_SessionId_Status",
                table: "CashProposals",
                columns: new[] { "SessionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CashProposals_VaultSessionId_Status",
                table: "CashProposals",
                columns: new[] { "VaultSessionId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_CH_Dest",
                table: "CashHandoffs",
                column: "DestinationSessionId",
                principalTable: "CashPositionSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CH_Dest",
                table: "CashHandoffs");

            migrationBuilder.DropTable(
                name: "CashProposals");

            migrationBuilder.DropIndex(
                name: "IX_CashPositionSessions_BusinessDate_TellerId_Status",
                table: "CashPositionSessions");

            migrationBuilder.DropIndex(
                name: "IX_CashHandoffs_DestinationSessionId",
                table: "CashHandoffs");

            migrationBuilder.DropColumn(
                name: "ActivatedAtUtc",
                table: "CashPositionSessions");

            migrationBuilder.DropColumn(
                name: "ReservedCash",
                table: "CashPositionSessions");

            migrationBuilder.DropColumn(
                name: "ScheduledStartAtMyanmar",
                table: "CashPositionSessions");

            migrationBuilder.DropColumn(
                name: "ScheduledEndAtMyanmar",
                table: "CashPositionSessions");

            migrationBuilder.DropColumn(
                name: "DestinationSessionId",
                table: "CashHandoffs");
        }
    }
}
