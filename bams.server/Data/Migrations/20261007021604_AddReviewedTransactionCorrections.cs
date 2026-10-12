using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bams.server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewedTransactionCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorrectionExternalRecoveryReference",
                table: "ReconciliationExceptions",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrectionRequestReason",
                table: "ReconciliationExceptions",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrectionRequestStatus",
                table: "ReconciliationExceptions",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CorrectionRequestedAtUtc",
                table: "ReconciliationExceptions",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CorrectionRequestedBy",
                table: "ReconciliationExceptions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrectionReviewNote",
                table: "ReconciliationExceptions",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CorrectionReviewedAtUtc",
                table: "ReconciliationExceptions",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CorrectionReviewedBy",
                table: "ReconciliationExceptions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RequestedCorrectionTransactionId",
                table: "ReconciliationExceptions",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CorrectionExternalRecoveryReference",
                table: "ReconciliationExceptions");

            migrationBuilder.DropColumn(
                name: "CorrectionRequestReason",
                table: "ReconciliationExceptions");

            migrationBuilder.DropColumn(
                name: "CorrectionRequestStatus",
                table: "ReconciliationExceptions");

            migrationBuilder.DropColumn(
                name: "CorrectionRequestedAtUtc",
                table: "ReconciliationExceptions");

            migrationBuilder.DropColumn(
                name: "CorrectionRequestedBy",
                table: "ReconciliationExceptions");

            migrationBuilder.DropColumn(
                name: "CorrectionReviewNote",
                table: "ReconciliationExceptions");

            migrationBuilder.DropColumn(
                name: "CorrectionReviewedAtUtc",
                table: "ReconciliationExceptions");

            migrationBuilder.DropColumn(
                name: "CorrectionReviewedBy",
                table: "ReconciliationExceptions");

            migrationBuilder.DropColumn(
                name: "RequestedCorrectionTransactionId",
                table: "ReconciliationExceptions");
        }
    }
}
