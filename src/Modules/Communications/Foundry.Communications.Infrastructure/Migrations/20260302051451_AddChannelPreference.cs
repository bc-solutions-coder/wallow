using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Foundry.Communications.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddChannelPreference : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "channel_preferences",
            schema: "communications",
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
            name: "sms_messages",
            schema: "communications",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                body = table.Column<string>(type: "text", nullable: false),
                status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                retry_count = table.Column<int>(type: "integer", nullable: false),
                to_phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
            schema: "communications",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                is_opted_in = table.Column<bool>(type: "boolean", nullable: false),
                phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_sms_preferences", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_channel_preferences_tenant_id",
            schema: "communications",
            table: "channel_preferences",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "IX_channel_preferences_tenant_id_user_id",
            schema: "communications",
            table: "channel_preferences",
            columns: new[] { "tenant_id", "user_id" });

        migrationBuilder.CreateIndex(
            name: "IX_channel_preferences_tenant_id_user_id_channel_type_notifica~",
            schema: "communications",
            table: "channel_preferences",
            columns: new[] { "tenant_id", "user_id", "channel_type", "notification_type" },
            unique: true);

        // Data migration: copy email_preferences into channel_preferences
        // Must run after the unique index is created for ON CONFLICT to work
        migrationBuilder.Sql("""
            INSERT INTO communications.channel_preferences (id, tenant_id, user_id, channel_type, notification_type, is_enabled, created_at, updated_at, created_by, updated_by)
            SELECT
                gen_random_uuid(),
                tenant_id,
                user_id,
                'Email',
                CASE notification_type
                    WHEN 'TaskAssigned' THEN 'task_assigned'
                    WHEN 'TaskCompleted' THEN 'task_completed'
                    WHEN 'BillingInvoice' THEN 'billing_invoice'
                    WHEN 'SystemNotification' THEN 'system_alert'
                    ELSE notification_type
                END,
                is_enabled,
                created_at,
                updated_at,
                created_by,
                updated_by
            FROM communications.email_preferences
            ON CONFLICT (tenant_id, user_id, channel_type, notification_type) DO NOTHING;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_sms_messages_created_at",
            schema: "communications",
            table: "sms_messages",
            column: "created_at");

        migrationBuilder.CreateIndex(
            name: "IX_sms_messages_status",
            schema: "communications",
            table: "sms_messages",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "IX_sms_messages_tenant_id",
            schema: "communications",
            table: "sms_messages",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "IX_sms_preferences_tenant_id",
            schema: "communications",
            table: "sms_preferences",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "IX_sms_preferences_tenant_id_user_id",
            schema: "communications",
            table: "sms_preferences",
            columns: new[] { "tenant_id", "user_id" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Reverse data migration: copy channel_preferences back to email_preferences
        migrationBuilder.Sql("""
            INSERT INTO communications.email_preferences (id, tenant_id, user_id, notification_type, is_enabled, created_at, updated_at, created_by, updated_by)
            SELECT
                gen_random_uuid(),
                tenant_id,
                user_id,
                CASE notification_type
                    WHEN 'task_assigned' THEN 'TaskAssigned'
                    WHEN 'task_completed' THEN 'TaskCompleted'
                    WHEN 'billing_invoice' THEN 'BillingInvoice'
                    WHEN 'system_alert' THEN 'SystemNotification'
                    ELSE notification_type
                END,
                is_enabled,
                created_at,
                updated_at,
                created_by,
                updated_by
            FROM communications.channel_preferences
            WHERE channel_type = 'Email'
            ON CONFLICT (tenant_id, user_id, notification_type) DO NOTHING;
            """);

        migrationBuilder.DropTable(
            name: "channel_preferences",
            schema: "communications");

        migrationBuilder.DropTable(
            name: "sms_messages",
            schema: "communications");

        migrationBuilder.DropTable(
            name: "sms_preferences",
            schema: "communications");
    }
}
