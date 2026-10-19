using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bams.server.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReconciliationForcePassHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MySQL requires an index on ExceptionId while its foreign key remains in place.
            // Keep a supporting index until the replacement under the EF index name exists.
            CreateSupportingIndex(migrationBuilder);

            migrationBuilder.DropIndex(
                name: "IX_ReconciliationExclusions_ExceptionId",
                table: "ReconciliationExclusions");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationExclusions_ExceptionId",
                table: "ReconciliationExclusions",
                column: "ExceptionId");

            DropSupportingIndex(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            CreateSupportingIndex(migrationBuilder);

            migrationBuilder.DropIndex(
                name: "IX_ReconciliationExclusions_ExceptionId",
                table: "ReconciliationExclusions");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationExclusions_ExceptionId",
                table: "ReconciliationExclusions",
                column: "ExceptionId",
                unique: true);

            DropSupportingIndex(migrationBuilder);
        }

        private const string SupportingIndexName = "IX_ReconciliationExclusions_ExceptionId_Support";

        private static void CreateSupportingIndex(MigrationBuilder migrationBuilder) =>
            migrationBuilder.CreateIndex(
                name: SupportingIndexName,
                table: "ReconciliationExclusions",
                column: "ExceptionId");

        private static void DropSupportingIndex(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropIndex(
                name: SupportingIndexName,
                table: "ReconciliationExclusions");
    }
}
