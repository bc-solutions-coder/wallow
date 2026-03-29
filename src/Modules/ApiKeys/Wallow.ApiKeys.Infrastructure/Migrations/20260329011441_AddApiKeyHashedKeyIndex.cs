using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallow.ApiKeys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApiKeyHashedKeyIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_api_keys_hashed_key",
                schema: "apikeys",
                table: "api_keys",
                column: "hashed_key");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_api_keys_hashed_key",
                schema: "apikeys",
                table: "api_keys");
        }
    }
}
