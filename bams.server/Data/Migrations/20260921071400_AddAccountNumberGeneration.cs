using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace bams.server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountNumberGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountNumberGenerations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    AccountTypeId = table.Column<long>(type: "bigint", nullable: false),
                    GenerationPeriod = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    LastSequenceNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountNumberGenerations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountNumberGenerations_AccountTypes_AccountTypeId",
                        column: x => x.AccountTypeId,
                        principalTable: "AccountTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AccountNumberGenerations_AccountTypeId_GenerationPeriod",
                table: "AccountNumberGenerations",
                columns: new[] { "AccountTypeId", "GenerationPeriod" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountNumberGenerations");
        }
    }
}
