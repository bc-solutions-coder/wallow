using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallow.Billing.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddTenantScopedInvoiceNumberIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_invoices_invoice_number",
            schema: "billing",
            table: "invoices");

        migrationBuilder.CreateIndex(
            name: "ix_billing_invoices_tenant_invoice_number",
            schema: "billing",
            table: "invoices",
            columns: new[] { "tenant_id", "invoice_number" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_billing_invoices_tenant_invoice_number",
            schema: "billing",
            table: "invoices");

        migrationBuilder.CreateIndex(
            name: "IX_invoices_invoice_number",
            schema: "billing",
            table: "invoices",
            column: "invoice_number",
            unique: true);
    }
}
