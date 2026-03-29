using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallow.Inquiries.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inquiries");

            migrationBuilder.CreateTable(
                name: "inquiries",
                schema: "inquiries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    phone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    company = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    submitter_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    project_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    budget_range = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    timeline = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    message = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    submitter_ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inquiries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inquiry_comments",
                schema: "inquiries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inquiry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    author_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    is_internal = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inquiry_comments", x => x.id);
                    table.ForeignKey(
                        name: "FK_inquiry_comments_inquiries_inquiry_id",
                        column: x => x.inquiry_id,
                        principalSchema: "inquiries",
                        principalTable: "inquiries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inquiries_created_at",
                schema: "inquiries",
                table: "inquiries",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_inquiries_email",
                schema: "inquiries",
                table: "inquiries",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "IX_inquiries_status",
                schema: "inquiries",
                table: "inquiries",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_inquiries_tenant_id",
                schema: "inquiries",
                table: "inquiries",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_inquiry_comments_inquiry_id",
                schema: "inquiries",
                table: "inquiry_comments",
                column: "inquiry_id");

            migrationBuilder.CreateIndex(
                name: "IX_inquiry_comments_tenant_id",
                schema: "inquiries",
                table: "inquiry_comments",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inquiry_comments",
                schema: "inquiries");

            migrationBuilder.DropTable(
                name: "inquiries",
                schema: "inquiries");
        }
    }
}
