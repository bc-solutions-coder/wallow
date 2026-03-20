using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallow.Billing.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AbsorbMeteringEntities : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "meter_definitions",
            schema: "billing",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                aggregation = table.Column<int>(type: "integer", nullable: false),
                is_billable = table.Column<bool>(type: "boolean", nullable: false),
                valkey_key_pattern = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_meter_definitions", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "quota_definitions",
            schema: "billing",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                meter_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                plan_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                limit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                period = table.Column<int>(type: "integer", nullable: false),
                on_exceeded = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_quota_definitions", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "usage_records",
            schema: "billing",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                meter_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                period_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                period_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                flushed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_usage_records", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_meter_definitions_code",
            schema: "billing",
            table: "meter_definitions",
            column: "code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_quota_definitions_meter_code",
            schema: "billing",
            table: "quota_definitions",
            column: "meter_code");

        migrationBuilder.CreateIndex(
            name: "IX_quota_definitions_plan_code",
            schema: "billing",
            table: "quota_definitions",
            column: "plan_code");

        migrationBuilder.CreateIndex(
            name: "IX_quota_definitions_tenant_id",
            schema: "billing",
            table: "quota_definitions",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "IX_quota_definitions_tenant_id_meter_code",
            schema: "billing",
            table: "quota_definitions",
            columns: new[] { "tenant_id", "meter_code" },
            unique: true,
            filter: "plan_code IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_usage_records_meter_code",
            schema: "billing",
            table: "usage_records",
            column: "meter_code");

        migrationBuilder.CreateIndex(
            name: "IX_usage_records_period_start",
            schema: "billing",
            table: "usage_records",
            column: "period_start");

        migrationBuilder.CreateIndex(
            name: "IX_usage_records_tenant_id",
            schema: "billing",
            table: "usage_records",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "IX_usage_records_tenant_id_meter_code_period_start_period_end",
            schema: "billing",
            table: "usage_records",
            columns: new[] { "tenant_id", "meter_code", "period_start", "period_end" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "meter_definitions",
            schema: "billing");

        migrationBuilder.DropTable(
            name: "quota_definitions",
            schema: "billing");

        migrationBuilder.DropTable(
            name: "usage_records",
            schema: "billing");
    }
}
