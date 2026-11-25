using System;
using bams.server.Constants;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace bams.server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    UniqueCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    ValueType = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    ManagerEditable = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AuditorEditable = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    OfficerEditable = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PersonalPreferences",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    SettingId = table.Column<long>(type: "bigint", nullable: false),
                    Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ValueSet = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    UpdateKey = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonalPreferences_Settings_SettingId",
                        column: x => x.SettingId,
                        principalTable: "Settings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonalPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalPreferences_SettingId",
                table: "PersonalPreferences",
                column: "SettingId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalPreferences_UserId_SettingId",
                table: "PersonalPreferences",
                columns: new[] { "UserId", "SettingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Settings_UniqueCode",
                table: "Settings",
                column: "UniqueCode",
                unique: true);

            // Seed the "Default Vault Opening" setting; only managers may edit it.
            migrationBuilder.InsertData(
                table: "Settings",
                columns: new[] { "Name", "UniqueCode", "Description", "ValueType", "ManagerEditable", "AuditorEditable", "OfficerEditable" },
                values: new object[]
                {
                    PreferenceConstants.DefaultVaultOpeningName,
                    PreferenceConstants.DefaultVaultOpeningCode,
                    PreferenceConstants.DefaultVaultOpeningDescription,
                    PreferenceConstants.DecimalValueType,
                    true,
                    false,
                    false
                });

            // Backfill one row per existing user for the new setting: enabled only for manager users, no value yet.
            // Roles are matched by code because role ids differ between databases.
            migrationBuilder.Sql($@"
INSERT INTO PersonalPreferences (UserId, SettingId, Enabled, ValueSet, UpdateKey, CreatedAt, UpdatedAt)
SELECT u.Id,
       s.Id,
       EXISTS (SELECT 1 FROM UserRoles ur INNER JOIN Roles r ON r.Id = ur.RoleId
               WHERE ur.UserId = u.Id AND r.Code = '{SecurityConstants.ManagerRole}'),
       NULL,
       UUID(),
       UTC_TIMESTAMP(6),
       UTC_TIMESTAMP(6)
FROM Users u
CROSS JOIN Settings s
WHERE s.UniqueCode = '{PreferenceConstants.DefaultVaultOpeningCode}';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PersonalPreferences");

            migrationBuilder.DropTable(
                name: "Settings");
        }
    }
}
