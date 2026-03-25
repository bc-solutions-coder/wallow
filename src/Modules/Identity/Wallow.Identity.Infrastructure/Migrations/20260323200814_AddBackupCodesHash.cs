using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallow.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBackupCodesHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_keys",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "client_brandings",
                schema: "identity");

            migrationBuilder.AddColumn<string>(
                name: "backup_codes_hash",
                schema: "identity",
                table: "users",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "mfa_enabled",
                schema: "identity",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "mfa_method",
                schema: "identity",
                table: "users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "totp_secret_encrypted",
                schema: "identity",
                table: "users",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "backup_codes_hash",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "mfa_enabled",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "mfa_method",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "totp_secret_encrypted",
                schema: "identity",
                table: "users");

            migrationBuilder.CreateTable(
                name: "api_keys",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    hashed_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false),
                    service_account_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    scopes = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_keys", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "client_brandings",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    logo_storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tagline = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    theme_json = table.Column<string>(type: "jsonb", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_brandings", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_service_account_id",
                schema: "identity",
                table: "api_keys",
                column: "service_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_tenant_id",
                schema: "identity",
                table: "api_keys",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_client_brandings_client_id",
                schema: "identity",
                table: "client_brandings",
                column: "client_id",
                unique: true);
        }
    }
}
