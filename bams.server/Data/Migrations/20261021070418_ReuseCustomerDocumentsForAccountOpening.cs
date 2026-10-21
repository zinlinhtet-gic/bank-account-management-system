using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bams.server.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReuseCustomerDocumentsForAccountOpening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SourceCustomerDocumentId",
                table: "AccountDocuments",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountDocuments_SourceCustomerDocumentId",
                table: "AccountDocuments",
                column: "SourceCustomerDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_AccountDocuments_CustomerDocuments_SourceCustomerDocumentId",
                table: "AccountDocuments",
                column: "SourceCustomerDocumentId",
                principalTable: "CustomerDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountDocuments_CustomerDocuments_SourceCustomerDocumentId",
                table: "AccountDocuments");

            migrationBuilder.DropIndex(
                name: "IX_AccountDocuments_SourceCustomerDocumentId",
                table: "AccountDocuments");

            migrationBuilder.DropColumn(
                name: "SourceCustomerDocumentId",
                table: "AccountDocuments");
        }
    }
}
