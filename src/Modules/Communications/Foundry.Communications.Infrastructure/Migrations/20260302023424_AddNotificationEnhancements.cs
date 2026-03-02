using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Foundry.Communications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "communications");

            migrationBuilder.CreateTable(
                name: "announcement_dismissals",
                schema: "communications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    announcement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dismissed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_announcement_dismissals", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "announcements",
                schema: "communications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    target = table.Column<int>(type: "integer", nullable: false),
                    target_value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    publish_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_pinned = table.Column<bool>(type: "boolean", nullable: false),
                    is_dismissible = table.Column<bool>(type: "boolean", nullable: false),
                    action_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    action_label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_announcements", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "changelog_entries",
                schema: "communications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    released_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_published = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_changelog_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "email_messages",
                schema: "communications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    from_address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "email_preferences",
                schema: "communications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_preferences", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                schema: "communications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    action_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    source_module = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "changelog_items",
                schema: "communications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_changelog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_changelog_items_changelog_entries_entry_id",
                        column: x => x.entry_id,
                        principalSchema: "communications",
                        principalTable: "changelog_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_announcement_dismissals_announcement_id",
                schema: "communications",
                table: "announcement_dismissals",
                column: "announcement_id");

            migrationBuilder.CreateIndex(
                name: "IX_announcement_dismissals_announcement_id_user_id",
                schema: "communications",
                table: "announcement_dismissals",
                columns: new[] { "announcement_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_announcement_dismissals_user_id",
                schema: "communications",
                table: "announcement_dismissals",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_announcements_expires_at",
                schema: "communications",
                table: "announcements",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_announcements_publish_at",
                schema: "communications",
                table: "announcements",
                column: "publish_at");

            migrationBuilder.CreateIndex(
                name: "IX_announcements_status",
                schema: "communications",
                table: "announcements",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_announcements_target",
                schema: "communications",
                table: "announcements",
                column: "target");

            migrationBuilder.CreateIndex(
                name: "IX_changelog_entries_is_published",
                schema: "communications",
                table: "changelog_entries",
                column: "is_published");

            migrationBuilder.CreateIndex(
                name: "IX_changelog_entries_released_at",
                schema: "communications",
                table: "changelog_entries",
                column: "released_at");

            migrationBuilder.CreateIndex(
                name: "IX_changelog_entries_version",
                schema: "communications",
                table: "changelog_entries",
                column: "version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_changelog_items_entry_id",
                schema: "communications",
                table: "changelog_items",
                column: "entry_id");

            migrationBuilder.CreateIndex(
                name: "IX_email_messages_created_at",
                schema: "communications",
                table: "email_messages",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_email_messages_status",
                schema: "communications",
                table: "email_messages",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_email_messages_tenant_id",
                schema: "communications",
                table: "email_messages",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_email_preferences_tenant_id",
                schema: "communications",
                table: "email_preferences",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_email_preferences_tenant_id_user_id",
                schema: "communications",
                table: "email_preferences",
                columns: new[] { "tenant_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_email_preferences_tenant_id_user_id_notification_type",
                schema: "communications",
                table: "email_preferences",
                columns: new[] { "tenant_id", "user_id", "notification_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_created_at",
                schema: "communications",
                table: "notifications",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_tenant_id",
                schema: "communications",
                table: "notifications",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_user_id",
                schema: "communications",
                table: "notifications",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "announcement_dismissals",
                schema: "communications");

            migrationBuilder.DropTable(
                name: "announcements",
                schema: "communications");

            migrationBuilder.DropTable(
                name: "changelog_items",
                schema: "communications");

            migrationBuilder.DropTable(
                name: "email_messages",
                schema: "communications");

            migrationBuilder.DropTable(
                name: "email_preferences",
                schema: "communications");

            migrationBuilder.DropTable(
                name: "notifications",
                schema: "communications");

            migrationBuilder.DropTable(
                name: "changelog_entries",
                schema: "communications");
        }
    }
}
