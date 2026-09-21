using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace bams.server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountCreationDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountDocuments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    AccountId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentType = table.Column<int>(type: "int", nullable: false),
                    DocumentNumber = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    OriginalFileName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    FileReference = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    ContentType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountDocuments_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AccountTypeRequiredDocuments",
                columns: table => new
                {
                    AccountTypeId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountTypeRequiredDocuments", x => new { x.AccountTypeId, x.DocumentType });
                    table.ForeignKey(
                        name: "FK_AccountTypeRequiredDocuments_AccountTypes_AccountTypeId",
                        column: x => x.AccountTypeId,
                        principalTable: "AccountTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AccountDocuments_AccountId_DocumentType",
                table: "AccountDocuments",
                columns: new[] { "AccountId", "DocumentType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountDocuments");

            migrationBuilder.DropTable(
                name: "AccountTypeRequiredDocuments");
        }
    }
}
