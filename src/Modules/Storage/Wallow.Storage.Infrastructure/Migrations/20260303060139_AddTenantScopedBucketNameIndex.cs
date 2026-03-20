using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallow.Storage.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddTenantScopedBucketNameIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_buckets_name",
            schema: "storage",
            table: "buckets");

        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAt",
            schema: "storage",
            table: "files",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

        migrationBuilder.AddColumn<Guid>(
            name: "CreatedBy",
            schema: "storage",
            table: "files",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "UpdatedAt",
            schema: "storage",
            table: "files",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "UpdatedBy",
            schema: "storage",
            table: "files",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "CreatedBy",
            schema: "storage",
            table: "buckets",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "UpdatedAt",
            schema: "storage",
            table: "buckets",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "UpdatedBy",
            schema: "storage",
            table: "buckets",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_storage_buckets_tenant_name",
            schema: "storage",
            table: "buckets",
            columns: new[] { "tenant_id", "name" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_storage_buckets_tenant_name",
            schema: "storage",
            table: "buckets");

        migrationBuilder.DropColumn(
            name: "CreatedAt",
            schema: "storage",
            table: "files");

        migrationBuilder.DropColumn(
            name: "CreatedBy",
            schema: "storage",
            table: "files");

        migrationBuilder.DropColumn(
            name: "UpdatedAt",
            schema: "storage",
            table: "files");

        migrationBuilder.DropColumn(
            name: "UpdatedBy",
            schema: "storage",
            table: "files");

        migrationBuilder.DropColumn(
            name: "CreatedBy",
            schema: "storage",
            table: "buckets");

        migrationBuilder.DropColumn(
            name: "UpdatedAt",
            schema: "storage",
            table: "buckets");

        migrationBuilder.DropColumn(
            name: "UpdatedBy",
            schema: "storage",
            table: "buckets");

        migrationBuilder.CreateIndex(
            name: "IX_buckets_name",
            schema: "storage",
            table: "buckets",
            column: "name",
            unique: true);
    }
}
