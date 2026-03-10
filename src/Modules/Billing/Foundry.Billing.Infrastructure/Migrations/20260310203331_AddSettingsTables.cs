using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Foundry.Billing.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddSettingsTables : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tenant_settings",
            schema: "billing",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                module_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                setting_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                value = table.Column<string>(type: "TEXT", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tenant_settings", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "user_settings",
            schema: "billing",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                module_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                setting_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                value = table.Column<string>(type: "TEXT", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_settings", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_tenant_settings_tenant_id_module_key_setting_key",
            schema: "billing",
            table: "tenant_settings",
            columns: new[] { "tenant_id", "module_key", "setting_key" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_user_settings_tenant_id_user_id_module_key_setting_key",
            schema: "billing",
            table: "user_settings",
            columns: new[] { "tenant_id", "user_id", "module_key", "setting_key" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "tenant_settings",
            schema: "billing");

        migrationBuilder.DropTable(
            name: "user_settings",
            schema: "billing");
    }
}
