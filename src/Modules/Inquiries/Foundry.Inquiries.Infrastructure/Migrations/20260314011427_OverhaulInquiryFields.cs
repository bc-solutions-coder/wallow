using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Foundry.Inquiries.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OverhaulInquiryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "phone",
                schema: "inquiries",
                table: "inquiries",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "submitter_id",
                schema: "inquiries",
                table: "inquiries",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "phone",
                schema: "inquiries",
                table: "inquiries");

            migrationBuilder.DropColumn(
                name: "submitter_id",
                schema: "inquiries",
                table: "inquiries");
        }
    }
}
