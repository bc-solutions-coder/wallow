using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallow.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantScopedIdentityIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasPassword",
                schema: "identity",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MfaGraceDeadline",
                schema: "identity",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "archived_at",
                schema: "identity",
                table: "organizations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "archived_by",
                schema: "identity",
                table: "organizations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "invitations",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    token = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    accepted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invitations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "membership_requests",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email_domain = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    resolved_organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_membership_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "organization_branding",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    logo_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    primary_color = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    accent_color = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_branding", x => x.id);
                    table.ForeignKey(
                        name: "FK_organization_branding_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "organization_domains",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    domain = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false),
                    verification_token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_domains", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "organization_settings",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    require_mfa = table.Column<bool>(type: "boolean", nullable: false),
                    allow_passwordless_login = table.Column<bool>(type: "boolean", nullable: false),
                    mfa_grace_period_days = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_settings", x => x.id);
                    table.ForeignKey(
                        name: "FK_organization_settings_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_tenant_id_id",
                schema: "identity",
                table: "users",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_users_tenant_id_is_active",
                schema: "identity",
                table: "users",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_users_tenant_id_normalized_email",
                schema: "identity",
                table: "users",
                columns: new[] { "tenant_id", "normalized_email" });

            migrationBuilder.CreateIndex(
                name: "IX_roles_tenant_id_normalized_name",
                schema: "identity",
                table: "roles",
                columns: new[] { "tenant_id", "normalized_name" });

            migrationBuilder.CreateIndex(
                name: "IX_organization_members_organization_id_user_id",
                schema: "identity",
                table: "organization_members",
                columns: new[] { "organization_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_organization_members_user_id",
                schema: "identity",
                table: "organization_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_invitations_tenant_id",
                schema: "identity",
                table: "invitations",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_invitations_token",
                schema: "identity",
                table: "invitations",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_membership_requests_tenant_id",
                schema: "identity",
                table: "membership_requests",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_membership_requests_user_id",
                schema: "identity",
                table: "membership_requests",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_organization_branding_organization_id",
                schema: "identity",
                table: "organization_branding",
                column: "organization_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_branding_tenant_id",
                schema: "identity",
                table: "organization_branding",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_organization_domains_domain",
                schema: "identity",
                table: "organization_domains",
                column: "domain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_domains_organization_id",
                schema: "identity",
                table: "organization_domains",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_organization_domains_tenant_id",
                schema: "identity",
                table: "organization_domains",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_organization_settings_organization_id",
                schema: "identity",
                table: "organization_settings",
                column: "organization_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_settings_tenant_id",
                schema: "identity",
                table: "organization_settings",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invitations",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "membership_requests",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "organization_branding",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "organization_domains",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "organization_settings",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "IX_users_tenant_id_id",
                schema: "identity",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_tenant_id_is_active",
                schema: "identity",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_tenant_id_normalized_email",
                schema: "identity",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_roles_tenant_id_normalized_name",
                schema: "identity",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "IX_organization_members_organization_id_user_id",
                schema: "identity",
                table: "organization_members");

            migrationBuilder.DropIndex(
                name: "IX_organization_members_user_id",
                schema: "identity",
                table: "organization_members");

            migrationBuilder.DropColumn(
                name: "HasPassword",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "MfaGraceDeadline",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "archived_at",
                schema: "identity",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "archived_by",
                schema: "identity",
                table: "organizations");
        }
    }
}
