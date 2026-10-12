using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace bams.server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCashHandoffCustody : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashHandoffs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    SessionId = table.Column<long>(type: "bigint", nullable: false),
                    CashCountId = table.Column<long>(type: "bigint", nullable: false),
                    BusinessDate = table.Column<DateTime>(type: "date", nullable: false),
                    SenderId = table.Column<long>(type: "bigint", nullable: false),
                    RecipientId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeclinedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashHandoffs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CH_Count",
                        column: x => x.CashCountId,
                        principalTable: "CashCounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CH_Recv",
                        column: x => x.RecipientId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CH_Send",
                        column: x => x.SenderId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CH_Sess",
                        column: x => x.SessionId,
                        principalTable: "CashPositionSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CashHandoffHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CashHandoffId = table.Column<long>(type: "bigint", nullable: false),
                    OldStatus = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    NewStatus = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    PreviousRecipientId = table.Column<long>(type: "bigint", nullable: true),
                    RecipientId = table.Column<long>(type: "bigint", nullable: true),
                    Note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    ActorId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashHandoffHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CHH_Actor",
                        column: x => x.ActorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CHH_Handoff",
                        column: x => x.CashHandoffId,
                        principalTable: "CashHandoffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CashHandoffHistories_ActorId",
                table: "CashHandoffHistories",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_CHH_Hist",
                table: "CashHandoffHistories",
                columns: new[] { "CashHandoffId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CashHandoffs_SenderId",
                table: "CashHandoffs",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_CashHandoffs_SessionId",
                table: "CashHandoffs",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CH_BD_ST",
                table: "CashHandoffs",
                columns: new[] { "BusinessDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CH_Count",
                table: "CashHandoffs",
                column: "CashCountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CH_R_ST",
                table: "CashHandoffs",
                columns: new[] { "RecipientId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashHandoffHistories");

            migrationBuilder.DropTable(
                name: "CashHandoffs");
        }
    }
}
