using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallow.ApiKeys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "apikeys");

            migrationBuilder.CreateTable(
                name: "api_keys",
                schema: "apikeys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_account_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    hashed_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false),
                    scopes = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_keys", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_hashed_key",
                schema: "apikeys",
                table: "api_keys",
                column: "hashed_key");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_service_account_id",
                schema: "apikeys",
                table: "api_keys",
                column: "service_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_tenant_id",
                schema: "apikeys",
                table: "api_keys",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_keys",
                schema: "apikeys");
        }
    }
}
