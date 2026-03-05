using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Foundry.Storage.Infrastructure.Migrations;

/// <inheritdoc />
public partial class SyncModelChanges : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Status",
            schema: "storage",
            table: "files",
            type: "integer",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Status",
            schema: "storage",
            table: "files");
    }
}
