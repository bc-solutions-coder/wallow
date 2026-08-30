using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallow.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformSuspension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "platform_suspended_at",
                schema: "identity",
                table: "registered_clients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "platform_suspended_by",
                schema: "identity",
                table: "registered_clients",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "platform_suspension_reason",
                schema: "identity",
                table: "registered_clients",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "platform_suspended_at",
                schema: "identity",
                table: "organizations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "platform_suspended_by",
                schema: "identity",
                table: "organizations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "platform_suspension_reason",
                schema: "identity",
                table: "organizations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "platform_suspended_at",
                schema: "identity",
                table: "registered_clients");

            migrationBuilder.DropColumn(
                name: "platform_suspended_by",
                schema: "identity",
                table: "registered_clients");

            migrationBuilder.DropColumn(
                name: "platform_suspension_reason",
                schema: "identity",
                table: "registered_clients");

            migrationBuilder.DropColumn(
                name: "platform_suspended_at",
                schema: "identity",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "platform_suspended_by",
                schema: "identity",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "platform_suspension_reason",
                schema: "identity",
                table: "organizations");
        }
    }
}
