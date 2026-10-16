using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bams.server.Data.Migrations
{
    /// <inheritdoc />
    public partial class CashProjectionSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EffectiveAccountBalances_ProposalId_AccountTransactionId",
                table: "EffectiveAccountBalances");

            migrationBuilder.AddColumn<string>(
                name: "ProposalSource",
                table: "EffectiveAccountBalances",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Account");

            migrationBuilder.CreateIndex(
                name: "IX_EffectiveAccountBalances_ProposalSource_ProposalId_AccountTr~",
                table: "EffectiveAccountBalances",
                columns: new[] { "ProposalSource", "ProposalId", "AccountTransactionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EffectiveAccountBalances_ProposalSource_ProposalId_AccountTr~",
                table: "EffectiveAccountBalances");

            migrationBuilder.DropColumn(
                name: "ProposalSource",
                table: "EffectiveAccountBalances");

            migrationBuilder.CreateIndex(
                name: "IX_EffectiveAccountBalances_ProposalId_AccountTransactionId",
                table: "EffectiveAccountBalances",
                columns: new[] { "ProposalId", "AccountTransactionId" },
                unique: true);
        }
    }
}
