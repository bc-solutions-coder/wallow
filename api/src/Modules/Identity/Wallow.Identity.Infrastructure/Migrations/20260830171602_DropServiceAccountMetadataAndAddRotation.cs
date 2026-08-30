using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallow.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropServiceAccountMetadataAndAddRotation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_account_metadata",
                schema: "identity");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_rotated_at",
                schema: "identity",
                table: "registered_clients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "last_rotated_by_user_id",
                schema: "identity",
                table: "registered_clients",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_rotated_at",
                schema: "identity",
                table: "registered_clients");

            migrationBuilder.DropColumn(
                name: "last_rotated_by_user_id",
                schema: "identity",
                table: "registered_clients");

            migrationBuilder.CreateTable(
                name: "service_account_metadata",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    scopes = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_account_metadata", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_service_account_metadata_client_id",
                schema: "identity",
                table: "service_account_metadata",
                column: "client_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_account_metadata_tenant_id",
                schema: "identity",
                table: "service_account_metadata",
                column: "tenant_id");
        }
    }
}
