using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Foundry.Inquiries.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "inquiries");

        migrationBuilder.CreateTable(
            name: "inquiries",
            schema: "inquiries",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                company = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                project_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                budget_range = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                timeline = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                message = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                submitter_ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_inquiries", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_inquiries_created_at",
            schema: "inquiries",
            table: "inquiries",
            column: "created_at");

        migrationBuilder.CreateIndex(
            name: "IX_inquiries_email",
            schema: "inquiries",
            table: "inquiries",
            column: "email");

        migrationBuilder.CreateIndex(
            name: "IX_inquiries_status",
            schema: "inquiries",
            table: "inquiries",
            column: "status");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "inquiries",
            schema: "inquiries");
    }
}
