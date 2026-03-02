using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Foundry.Communications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSmsMessageColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "failure_reason",
                schema: "communications",
                table: "sms_messages",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "from_phone_number",
                schema: "communications",
                table: "sms_messages",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "sent_at",
                schema: "communications",
                table: "sms_messages",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "failure_reason",
                schema: "communications",
                table: "sms_messages");

            migrationBuilder.DropColumn(
                name: "from_phone_number",
                schema: "communications",
                table: "sms_messages");

            migrationBuilder.DropColumn(
                name: "sent_at",
                schema: "communications",
                table: "sms_messages");
        }
    }
}
