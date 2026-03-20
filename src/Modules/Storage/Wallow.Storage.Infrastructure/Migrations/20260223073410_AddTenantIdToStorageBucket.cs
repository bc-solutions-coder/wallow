using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallow.Storage.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddTenantIdToStorageBucket : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "tenant_id",
            schema: "storage",
            table: "buckets",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.CreateIndex(
            name: "IX_buckets_tenant_id",
            schema: "storage",
            table: "buckets",
            column: "tenant_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_buckets_tenant_id",
            schema: "storage",
            table: "buckets");

        migrationBuilder.DropColumn(
            name: "tenant_id",
            schema: "storage",
            table: "buckets");
    }
}
