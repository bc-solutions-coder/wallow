using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallow.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOpenIddictTokenIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_ExpirationDate",
                schema: "identity",
                table: "OpenIddictTokens",
                column: "ExpirationDate");

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_Status_ExpirationDate",
                schema: "identity",
                table: "OpenIddictTokens",
                columns: new[] { "Status", "ExpirationDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OpenIddictTokens_ExpirationDate",
                schema: "identity",
                table: "OpenIddictTokens");

            migrationBuilder.DropIndex(
                name: "IX_OpenIddictTokens_Status_ExpirationDate",
                schema: "identity",
                table: "OpenIddictTokens");
        }
    }
}
