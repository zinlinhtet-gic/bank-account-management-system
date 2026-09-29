using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bams.server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReferersToAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowCitizen",
                table: "AccountTypes",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowForeigner",
                table: "AccountTypes",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "CitizenRequiredRefer",
                table: "AccountTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ForeignRequiredRefer",
                table: "AccountTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AccountReferers",
                columns: table => new
                {
                    AccountId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountReferers", x => new { x.AccountId, x.CustomerId });
                    table.ForeignKey(
                        name: "FK_AccountReferers_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountReferers_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AccountReferers_CustomerId",
                table: "AccountReferers",
                column: "CustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AccountReferers");
            migrationBuilder.DropColumn(name: "AllowCitizen", table: "AccountTypes");
            migrationBuilder.DropColumn(name: "AllowForeigner", table: "AccountTypes");
            migrationBuilder.DropColumn(name: "CitizenRequiredRefer", table: "AccountTypes");
            migrationBuilder.DropColumn(name: "ForeignRequiredRefer", table: "AccountTypes");
        }
    }
}
