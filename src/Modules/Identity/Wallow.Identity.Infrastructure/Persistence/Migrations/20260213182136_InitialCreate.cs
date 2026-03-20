using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

using System.Diagnostics.CodeAnalysis;

namespace Wallow.Identity.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
[ExcludeFromCodeCoverage]
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "identity");

        migrationBuilder.CreateTable(
            name: "api_scopes",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                is_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_api_scopes", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "scim_configurations",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                bearer_token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                token_prefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                token_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                last_sync_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                auto_activate_users = table.Column<bool>(type: "boolean", nullable: false),
                default_role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                deprovision_on_delete = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true)
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
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                operation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                resource_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                external_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                internal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                success = table.Column<bool>(type: "boolean", nullable: false),
                error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                request_body = table.Column<string>(type: "text", nullable: true),
                timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_scim_sync_logs", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "service_account_metadata",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                keycloak_client_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                scopes = table.Column<string>(type: "jsonb", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_service_account_metadata", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "sso_configurations",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                protocol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                saml_entity_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                saml_sso_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                saml_slo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                saml_certificate = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                saml_name_id_format = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                oidc_issuer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                oidc_client_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                oidc_client_secret = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                oidc_scopes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                email_attribute = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                first_name_attribute = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                last_name_attribute = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                groups_attribute = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                enforce_for_all_users = table.Column<bool>(type: "boolean", nullable: false),
                auto_provision_users = table.Column<bool>(type: "boolean", nullable: false),
                default_role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                sync_groups_as_roles = table.Column<bool>(type: "boolean", nullable: false),
                keycloak_idp_alias = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_sso_configurations", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_api_scopes_category",
            schema: "identity",
            table: "api_scopes",
            column: "category");

        migrationBuilder.CreateIndex(
            name: "IX_api_scopes_code",
            schema: "identity",
            table: "api_scopes",
            column: "code",
            unique: true);

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
            name: "IX_service_account_metadata_keycloak_client_id",
            schema: "identity",
            table: "service_account_metadata",
            column: "keycloak_client_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_service_account_metadata_tenant_id",
            schema: "identity",
            table: "service_account_metadata",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "IX_sso_configurations_keycloak_idp_alias",
            schema: "identity",
            table: "sso_configurations",
            column: "keycloak_idp_alias",
            unique: true,
            filter: "keycloak_idp_alias IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_sso_configurations_tenant_id",
            schema: "identity",
            table: "sso_configurations",
            column: "tenant_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "api_scopes",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "scim_configurations",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "scim_sync_logs",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "service_account_metadata",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "sso_configurations",
            schema: "identity");
    }
}
