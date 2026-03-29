using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallow.Storage.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "storage");

            migrationBuilder.CreateTable(
                name: "buckets",
                schema: "storage",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    access = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    max_file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    allowed_content_types = table.Column<string>(type: "jsonb", nullable: true),
                    retention_days = table.Column<int>(type: "integer", nullable: true),
                    retention_action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    versioning = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_buckets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_settings",
                schema: "storage",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    module_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    setting_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_settings",
                schema: "storage",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    module_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    setting_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "files",
                schema: "storage",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bucket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_public = table.Column<bool>(type: "boolean", nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_files", x => x.id);
                    table.ForeignKey(
                        name: "FK_files_buckets_bucket_id",
                        column: x => x.bucket_id,
                        principalSchema: "storage",
                        principalTable: "buckets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_buckets_tenant_id",
                schema: "storage",
                table: "buckets",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_storage_buckets_tenant_name",
                schema: "storage",
                table: "buckets",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_files_bucket_id",
                schema: "storage",
                table: "files",
                column: "bucket_id");

            migrationBuilder.CreateIndex(
                name: "IX_files_bucket_id_path",
                schema: "storage",
                table: "files",
                columns: new[] { "bucket_id", "path" });

            migrationBuilder.CreateIndex(
                name: "IX_files_storage_key",
                schema: "storage",
                table: "files",
                column: "storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_files_tenant_id",
                schema: "storage",
                table: "files",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_settings_tenant_id_module_key_setting_key",
                schema: "storage",
                table: "tenant_settings",
                columns: new[] { "tenant_id", "module_key", "setting_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_settings_tenant_id_user_id_module_key_setting_key",
                schema: "storage",
                table: "user_settings",
                columns: new[] { "tenant_id", "user_id", "module_key", "setting_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "files",
                schema: "storage");

            migrationBuilder.DropTable(
                name: "tenant_settings",
                schema: "storage");

            migrationBuilder.DropTable(
                name: "user_settings",
                schema: "storage");

            migrationBuilder.DropTable(
                name: "buckets",
                schema: "storage");
        }
    }
}
