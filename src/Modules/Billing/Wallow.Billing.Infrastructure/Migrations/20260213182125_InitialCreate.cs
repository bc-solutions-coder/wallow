using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallow.Billing.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "billing");

        migrationBuilder.CreateTable(
            name: "invoices",
            schema: "billing",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                invoice_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                custom_fields = table.Column<string>(type: "jsonb", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_invoices", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "payments",
            schema: "billing",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                method = table.Column<int>(type: "integer", nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                transaction_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                failure_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                custom_fields = table.Column<string>(type: "jsonb", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_payments", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "subscriptions",
            schema: "billing",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                plan_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                current_period_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                current_period_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                custom_fields = table.Column<string>(type: "jsonb", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_subscriptions", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "invoice_line_items",
            schema: "billing",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                quantity = table.Column<int>(type: "integer", nullable: false),
                line_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                line_total_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_invoice_line_items", x => x.id);
                table.ForeignKey(
                    name: "FK_invoice_line_items_invoices_invoice_id",
                    column: x => x.invoice_id,
                    principalSchema: "billing",
                    principalTable: "invoices",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_invoice_line_items_invoice_id",
            schema: "billing",
            table: "invoice_line_items",
            column: "invoice_id");

        migrationBuilder.CreateIndex(
            name: "IX_invoices_invoice_number",
            schema: "billing",
            table: "invoices",
            column: "invoice_number",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_invoices_status",
            schema: "billing",
            table: "invoices",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "IX_invoices_tenant_id",
            schema: "billing",
            table: "invoices",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "IX_invoices_user_id",
            schema: "billing",
            table: "invoices",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "IX_payments_invoice_id",
            schema: "billing",
            table: "payments",
            column: "invoice_id");

        migrationBuilder.CreateIndex(
            name: "IX_payments_status",
            schema: "billing",
            table: "payments",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "IX_payments_tenant_id",
            schema: "billing",
            table: "payments",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "IX_payments_user_id",
            schema: "billing",
            table: "payments",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "IX_subscriptions_status",
            schema: "billing",
            table: "subscriptions",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "IX_subscriptions_tenant_id",
            schema: "billing",
            table: "subscriptions",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "IX_subscriptions_user_id",
            schema: "billing",
            table: "subscriptions",
            column: "user_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "invoice_line_items",
            schema: "billing");

        migrationBuilder.DropTable(
            name: "payments",
            schema: "billing");

        migrationBuilder.DropTable(
            name: "subscriptions",
            schema: "billing");

        migrationBuilder.DropTable(
            name: "invoices",
            schema: "billing");
    }
}
