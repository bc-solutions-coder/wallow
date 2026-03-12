using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Foundry.Notifications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "notifications");

            migrationBuilder.CreateTable(
                name: "channel_preferences",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    notification_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_channel_preferences", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "device_registrations",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    platform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    registered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_registrations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "email_messages",
                schema: "notifications",
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
                schema: "notifications",
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
                schema: "notifications",
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
                name: "push_messages",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_push_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sms_messages",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    from_phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
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
                    table.PrimaryKey("PK_sms_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sms_preferences",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_opted_in = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sms_preferences", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_push_configurations",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    platform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    encrypted_credentials = table.Column<string>(type: "text", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_push_configurations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_channel_preferences_tenant_id",
                schema: "notifications",
                table: "channel_preferences",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_channel_preferences_tenant_id_user_id",
                schema: "notifications",
                table: "channel_preferences",
                columns: new[] { "tenant_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_channel_preferences_tenant_id_user_id_channel_type_notifica~",
                schema: "notifications",
                table: "channel_preferences",
                columns: new[] { "tenant_id", "user_id", "channel_type", "notification_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_device_registrations_tenant_id",
                schema: "notifications",
                table: "device_registrations",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_device_registrations_token_tenant_id",
                schema: "notifications",
                table: "device_registrations",
                columns: new[] { "token", "tenant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_messages_created_at",
                schema: "notifications",
                table: "email_messages",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_email_messages_status",
                schema: "notifications",
                table: "email_messages",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_email_messages_tenant_id",
                schema: "notifications",
                table: "email_messages",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_email_preferences_tenant_id",
                schema: "notifications",
                table: "email_preferences",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_email_preferences_tenant_id_user_id",
                schema: "notifications",
                table: "email_preferences",
                columns: new[] { "tenant_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_email_preferences_tenant_id_user_id_notification_type",
                schema: "notifications",
                table: "email_preferences",
                columns: new[] { "tenant_id", "user_id", "notification_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_created_at",
                schema: "notifications",
                table: "notifications",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_tenant_id",
                schema: "notifications",
                table: "notifications",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_user_id",
                schema: "notifications",
                table: "notifications",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_push_messages_created_at",
                schema: "notifications",
                table: "push_messages",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_push_messages_status",
                schema: "notifications",
                table: "push_messages",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_push_messages_tenant_id",
                schema: "notifications",
                table: "push_messages",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_sms_messages_created_at",
                schema: "notifications",
                table: "sms_messages",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_sms_messages_status",
                schema: "notifications",
                table: "sms_messages",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_sms_messages_tenant_id",
                schema: "notifications",
                table: "sms_messages",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_sms_preferences_tenant_id",
                schema: "notifications",
                table: "sms_preferences",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_sms_preferences_tenant_id_user_id",
                schema: "notifications",
                table: "sms_preferences",
                columns: new[] { "tenant_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_push_configurations_tenant_id",
                schema: "notifications",
                table: "tenant_push_configurations",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_push_configurations_tenant_id_platform",
                schema: "notifications",
                table: "tenant_push_configurations",
                columns: new[] { "tenant_id", "platform" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "channel_preferences",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "device_registrations",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "email_messages",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "email_preferences",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notifications",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "push_messages",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "sms_messages",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "sms_preferences",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "tenant_push_configurations",
                schema: "notifications");
        }
    }
}
