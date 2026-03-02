using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Foundry.Communications.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddMessaging : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "conversations",
            schema: "communications",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                is_group = table.Column<bool>(type: "boolean", nullable: false),
                status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_conversations", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "messages",
            schema: "communications",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                sender_id = table.Column<Guid>(type: "uuid", nullable: false),
                body = table.Column<string>(type: "text", nullable: false),
                sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_messages", x => x.id);
                table.ForeignKey(
                    name: "FK_messages_conversations_conversation_id",
                    column: x => x.conversation_id,
                    principalSchema: "communications",
                    principalTable: "conversations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "participants",
            schema: "communications",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                last_read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_participants", x => x.id);
                table.ForeignKey(
                    name: "FK_participants_conversations_conversation_id",
                    column: x => x.conversation_id,
                    principalSchema: "communications",
                    principalTable: "conversations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_conversations_created_at",
            schema: "communications",
            table: "conversations",
            column: "created_at",
            descending: Array.Empty<bool>());

        migrationBuilder.CreateIndex(
            name: "IX_conversations_tenant_id",
            schema: "communications",
            table: "conversations",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "IX_messages_conversation_id",
            schema: "communications",
            table: "messages",
            column: "conversation_id");

        migrationBuilder.CreateIndex(
            name: "IX_messages_sender_id",
            schema: "communications",
            table: "messages",
            column: "sender_id");

        migrationBuilder.CreateIndex(
            name: "IX_participants_conversation_id",
            schema: "communications",
            table: "participants",
            column: "conversation_id");

        migrationBuilder.CreateIndex(
            name: "IX_participants_conversation_id_user_id",
            schema: "communications",
            table: "participants",
            columns: new[] { "conversation_id", "user_id" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "messages",
            schema: "communications");

        migrationBuilder.DropTable(
            name: "participants",
            schema: "communications");

        migrationBuilder.DropTable(
            name: "conversations",
            schema: "communications");
    }
}
