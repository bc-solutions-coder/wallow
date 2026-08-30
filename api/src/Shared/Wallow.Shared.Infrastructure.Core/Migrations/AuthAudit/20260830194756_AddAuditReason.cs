using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallow.Shared.Infrastructure.Core.Migrations.AuthAudit
{
    /// <inheritdoc />
    public partial class AddAuditReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Reason",
                schema: "auth_audit",
                table: "auth_audit_entries",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Reason",
                schema: "auth_audit",
                table: "auth_audit_entries");
        }
    }
}
