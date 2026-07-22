using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallow.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeOrgMemberRoleToEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Role now persists via EnumToStringConverter, which emits the PascalCase enum
            // name (Owner/Admin/Member). Normalize any pre-existing lowercase rows so they
            // round-trip back to the enum on read.
            migrationBuilder.Sql(
                "UPDATE identity.organization_members SET role = initcap(role);");

            migrationBuilder.AlterColumn<string>(
                name: "role",
                schema: "identity",
                table: "organization_members",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "role",
                schema: "identity",
                table: "organization_members",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            // Restore the lowercase role convention used before the enum conversion.
            migrationBuilder.Sql(
                "UPDATE identity.organization_members SET role = lower(role);");
        }
    }
}
