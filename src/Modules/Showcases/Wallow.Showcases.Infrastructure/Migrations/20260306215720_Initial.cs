using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallow.Showcases.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Initial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "showcases");

        migrationBuilder.CreateTable(
            name: "showcases",
            schema: "showcases",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "text", nullable: true),
                category = table.Column<int>(type: "integer", nullable: false),
                demo_url = table.Column<string>(type: "text", nullable: true),
                github_url = table.Column<string>(type: "text", nullable: true),
                video_url = table.Column<string>(type: "text", nullable: true),
                display_order = table.Column<int>(type: "integer", nullable: false),
                is_published = table.Column<bool>(type: "boolean", nullable: false),
                tags = table.Column<List<string>>(type: "text[]", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_showcases", x => x.id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "showcases",
            schema: "showcases");
    }
}
