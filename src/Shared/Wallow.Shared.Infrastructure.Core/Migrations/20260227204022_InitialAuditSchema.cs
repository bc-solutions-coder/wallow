using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallow.Shared.Infrastructure.Core.Migrations;

/// <inheritdoc />
public partial class InitialAuditSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "audit");

        migrationBuilder.CreateTable(
            name: "audit_entries",
            schema: "audit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EntityType = table.Column<string>(type: "text", nullable: false),
                EntityId = table.Column<string>(type: "text", nullable: false),
                Action = table.Column<string>(type: "text", nullable: false),
                OldValues = table.Column<string>(type: "jsonb", nullable: true),
                NewValues = table.Column<string>(type: "jsonb", nullable: true),
                UserId = table.Column<string>(type: "text", nullable: true),
                TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_audit_entries", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "audit_entries",
            schema: "audit");
    }
}
