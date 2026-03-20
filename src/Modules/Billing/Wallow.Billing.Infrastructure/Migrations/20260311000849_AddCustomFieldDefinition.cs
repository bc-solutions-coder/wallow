using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallow.Billing.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddCustomFieldDefinition : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "custom_field_definitions",
            schema: "billing",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                field_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                field_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                is_required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                validation_rules = table.Column<string>(type: "jsonb", nullable: true),
                options = table.Column<string>(type: "jsonb", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_custom_field_definitions", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_custom_field_definitions_tenant_entity_active",
            schema: "billing",
            table: "custom_field_definitions",
            columns: new[] { "tenant_id", "entity_type", "is_active" });

        migrationBuilder.CreateIndex(
            name: "ix_custom_field_definitions_tenant_entity_key",
            schema: "billing",
            table: "custom_field_definitions",
            columns: new[] { "tenant_id", "entity_type", "field_key" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_custom_field_definitions_tenant_id",
            schema: "billing",
            table: "custom_field_definitions",
            column: "tenant_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "custom_field_definitions",
            schema: "billing");
    }
}
