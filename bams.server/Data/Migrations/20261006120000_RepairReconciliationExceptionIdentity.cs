using bams.server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bams.server.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261006120000_RepairReconciliationExceptionIdentity")]
public sealed class RepairReconciliationExceptionIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Reassert the identity column so MySQL resets its next value beyond existing rows.
        migrationBuilder.Sql("ALTER TABLE `ReconciliationExceptions` MODIFY COLUMN `Id` BIGINT NOT NULL AUTO_INCREMENT;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Keep the identity invariant when rolling back application migrations.
    }
}
