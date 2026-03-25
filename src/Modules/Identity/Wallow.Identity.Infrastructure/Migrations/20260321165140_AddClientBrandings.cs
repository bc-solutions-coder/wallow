using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallow.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientBrandings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "client_brandings",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tagline = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    logo_storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    theme_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_brandings", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_client_brandings_client_id",
                schema: "identity",
                table: "client_brandings",
                column: "client_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "client_brandings",
                schema: "identity");
        }
    }
}
