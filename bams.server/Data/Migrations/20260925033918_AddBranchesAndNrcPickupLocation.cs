using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace bams.server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchesAndNrcPickupLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PickupBranchId",
                table: "NrcCashTransferDetails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PickupOtherBankId",
                table: "NrcCashTransferDetails",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    City = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_NrcCashTransferDetails_PickupBranchId",
                table: "NrcCashTransferDetails",
                column: "PickupBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_NrcCashTransferDetails_PickupOtherBankId",
                table: "NrcCashTransferDetails",
                column: "PickupOtherBankId");

            migrationBuilder.CreateIndex(
                name: "IX_Branches_Code",
                table: "Branches",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_NrcCashTransferDetails_Branches_PickupBranchId",
                table: "NrcCashTransferDetails",
                column: "PickupBranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NrcCashTransferDetails_OtherBanks_PickupOtherBankId",
                table: "NrcCashTransferDetails",
                column: "PickupOtherBankId",
                principalTable: "OtherBanks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NrcCashTransferDetails_Branches_PickupBranchId",
                table: "NrcCashTransferDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_NrcCashTransferDetails_OtherBanks_PickupOtherBankId",
                table: "NrcCashTransferDetails");

            migrationBuilder.DropTable(
                name: "Branches");

            migrationBuilder.DropIndex(
                name: "IX_NrcCashTransferDetails_PickupBranchId",
                table: "NrcCashTransferDetails");

            migrationBuilder.DropIndex(
                name: "IX_NrcCashTransferDetails_PickupOtherBankId",
                table: "NrcCashTransferDetails");

            migrationBuilder.DropColumn(
                name: "PickupBranchId",
                table: "NrcCashTransferDetails");

            migrationBuilder.DropColumn(
                name: "PickupOtherBankId",
                table: "NrcCashTransferDetails");
        }
    }
}
