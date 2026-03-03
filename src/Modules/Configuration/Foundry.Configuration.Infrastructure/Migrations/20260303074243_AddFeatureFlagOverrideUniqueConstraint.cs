using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Foundry.Configuration.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddFeatureFlagOverrideUniqueConstraint : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "ix_configuration_feature_flag_overrides_tenant_flag",
            schema: "configuration",
            table: "feature_flag_overrides",
            columns: new[] { "tenant_id", "flag_id" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_configuration_feature_flag_overrides_tenant_flag",
            schema: "configuration",
            table: "feature_flag_overrides");
    }
}
