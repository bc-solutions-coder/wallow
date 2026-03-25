using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallow.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameKeycloakColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sso_configurations_keycloak_idp_alias",
                schema: "identity",
                table: "sso_configurations");

            migrationBuilder.RenameColumn(
                name: "keycloak_idp_alias",
                schema: "identity",
                table: "sso_configurations",
                newName: "idp_alias");

            migrationBuilder.RenameColumn(
                name: "keycloak_client_id",
                schema: "identity",
                table: "service_account_metadata",
                newName: "client_id");

            migrationBuilder.RenameIndex(
                name: "IX_service_account_metadata_keycloak_client_id",
                schema: "identity",
                table: "service_account_metadata",
                newName: "IX_service_account_metadata_client_id");

            migrationBuilder.CreateIndex(
                name: "IX_sso_configurations_idp_alias",
                schema: "identity",
                table: "sso_configurations",
                column: "idp_alias",
                unique: true,
                filter: "idp_alias IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sso_configurations_idp_alias",
                schema: "identity",
                table: "sso_configurations");

            migrationBuilder.RenameColumn(
                name: "idp_alias",
                schema: "identity",
                table: "sso_configurations",
                newName: "keycloak_idp_alias");

            migrationBuilder.RenameColumn(
                name: "client_id",
                schema: "identity",
                table: "service_account_metadata",
                newName: "keycloak_client_id");

            migrationBuilder.RenameIndex(
                name: "IX_service_account_metadata_client_id",
                schema: "identity",
                table: "service_account_metadata",
                newName: "IX_service_account_metadata_keycloak_client_id");

            migrationBuilder.CreateIndex(
                name: "IX_sso_configurations_keycloak_idp_alias",
                schema: "identity",
                table: "sso_configurations",
                column: "keycloak_idp_alias",
                unique: true,
                filter: "keycloak_idp_alias IS NOT NULL");
        }
    }
}
