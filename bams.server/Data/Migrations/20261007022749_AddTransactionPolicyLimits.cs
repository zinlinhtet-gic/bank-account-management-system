using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bams.server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionPolicyLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DailyWithdrawalLimit",
                table: "AccountTypes",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumDepositAmount",
                table: "AccountTypes",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumWithdrawalAmount",
                table: "AccountTypes",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WeeklyTransactionLimit",
                table: "AccountTypes",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            // Products seeded before this migration were created with neutral terms (no balances, no limits). Give
            // them the bank-policy terms that ProductSeeder now seeds; products an administrator already configured
            // no longer have neutral terms and are left alone.
            BackfillSeededProductPolicy(migrationBuilder, "CURRENT", 10_000m, 1_000m, 1_000_000m, 5_000_000m, 1_000_000m, 1_000m);
            BackfillSeededProductPolicy(migrationBuilder, "NORMAL_SAVING", 10_000m, 10_000m, 2_000_000m, 10_000_000m, 1_000_000m, 1_000m);
            BackfillSeededProductPolicy(migrationBuilder, "SPECIAL_SAVING", 1_000_000m, 500_000m, 1_000_000m, 5_000_000m, 1_000_000m, 1_000m);
            BackfillSeededProductPolicy(migrationBuilder, "NORMAL_DEPOSIT", 10_000m, 0m, null, null, null, null);
            BackfillSeededProductPolicy(migrationBuilder, "SPECIAL_DEPOSIT", 1_000_000m, 0m, null, null, null, null);
            BackfillSeededProductPolicy(migrationBuilder, "HUNDRED_DAYS_DEPOSIT", 10_000_000m, 0m, null, null, null, null);
        }

        // Updates one seeded product that still has neutral terms. The minimum cash transaction amount is used for
        // both the minimum deposit and the minimum withdrawal.
        private static void BackfillSeededProductPolicy(
            MigrationBuilder migrationBuilder,
            string code,
            decimal minimumOpeningBalance,
            decimal minimumMaintainedBalance,
            decimal? dailyTransactionLimit,
            decimal? weeklyTransactionLimit,
            decimal? dailyWithdrawalLimit,
            decimal? minimumCashTransactionAmount)
        {
            static string ToSql(decimal? value) =>
                value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "NULL";

            migrationBuilder.Sql($"""
                UPDATE AccountTypes
                SET MinimumOpeningBalance = {ToSql(minimumOpeningBalance)},
                    MinimumMaintainedBalance = {ToSql(minimumMaintainedBalance)},
                    DailyTransactionLimit = {ToSql(dailyTransactionLimit)},
                    WeeklyTransactionLimit = {ToSql(weeklyTransactionLimit)},
                    DailyWithdrawalLimit = {ToSql(dailyWithdrawalLimit)},
                    MinimumDepositAmount = {ToSql(minimumCashTransactionAmount)},
                    MinimumWithdrawalAmount = {ToSql(minimumCashTransactionAmount)}
                WHERE Code = '{code}'
                  AND MinimumOpeningBalance = 0
                  AND MinimumMaintainedBalance = 0
                  AND DailyTransactionLimit IS NULL
                  AND MonthlyTransactionLimit IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DailyWithdrawalLimit",
                table: "AccountTypes");

            migrationBuilder.DropColumn(
                name: "MinimumDepositAmount",
                table: "AccountTypes");

            migrationBuilder.DropColumn(
                name: "MinimumWithdrawalAmount",
                table: "AccountTypes");

            migrationBuilder.DropColumn(
                name: "WeeklyTransactionLimit",
                table: "AccountTypes");
        }
    }
}
