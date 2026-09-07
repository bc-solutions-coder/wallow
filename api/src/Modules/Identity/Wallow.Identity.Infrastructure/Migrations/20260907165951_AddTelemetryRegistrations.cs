using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallow.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTelemetryRegistrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "telemetry_registrations",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    acknowledged_revision = table.Column<long>(type: "bigint", nullable: false),
                    credential_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    verifier = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    failure = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false),
                    access_state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    previous_credential_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    previous_verifier = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    rotation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    previous_credential_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telemetry_registrations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_registrations_next_attempt_at",
                schema: "identity",
                table: "telemetry_registrations",
                column: "next_attempt_at");

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_registrations_organization_id",
                schema: "identity",
                table: "telemetry_registrations",
                column: "organization_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "telemetry_registrations",
                schema: "identity");
        }
    }
}
