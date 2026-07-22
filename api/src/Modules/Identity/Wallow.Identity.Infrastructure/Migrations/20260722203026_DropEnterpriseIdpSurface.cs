using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallow.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropEnterpriseIdpSurface : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "initial_access_tokens",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "membership_requests",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "organization_domains",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "scim_configurations",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "scim_sync_logs",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "sso_configurations",
                schema: "identity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "initial_access_tokens",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    token_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_initial_access_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "membership_requests",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    email_domain = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    resolved_organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_membership_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "organization_domains",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    domain = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    verification_token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_domains", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "scim_configurations",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    auto_activate_users = table.Column<bool>(type: "boolean", nullable: false),
                    bearer_token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    default_role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    deprovision_on_delete = table.Column<bool>(type: "boolean", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    last_sync_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    token_prefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scim_configurations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "scim_sync_logs",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    external_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    internal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    operation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    request_body = table.Column<string>(type: "text", nullable: true),
                    resource_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scim_sync_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sso_configurations",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    auto_provision_users = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    default_role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email_attribute = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    enforce_for_all_users = table.Column<bool>(type: "boolean", nullable: false),
                    first_name_attribute = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    groups_attribute = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    idp_alias = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    last_name_attribute = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    oidc_client_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    oidc_client_secret = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    oidc_issuer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    oidc_scopes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    protocol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    saml_certificate = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    saml_entity_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    saml_name_id_format = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    saml_slo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    saml_sso_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sync_groups_as_roles = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sso_configurations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_initial_access_tokens_token_hash",
                schema: "identity",
                table: "initial_access_tokens",
                column: "token_hash",
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
                name: "IX_scim_configurations_tenant_id",
                schema: "identity",
                table: "scim_configurations",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_scim_sync_logs_tenant_id",
                schema: "identity",
                table: "scim_sync_logs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_scim_sync_logs_tenant_id_timestamp",
                schema: "identity",
                table: "scim_sync_logs",
                columns: new[] { "tenant_id", "timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_scim_sync_logs_timestamp",
                schema: "identity",
                table: "scim_sync_logs",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_sso_configurations_idp_alias",
                schema: "identity",
                table: "sso_configurations",
                column: "idp_alias",
                unique: true,
                filter: "idp_alias IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_sso_configurations_tenant_id",
                schema: "identity",
                table: "sso_configurations",
                column: "tenant_id");
        }
    }
}
