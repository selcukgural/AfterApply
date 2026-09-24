using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProtectSharedCompanyAndJobData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WebsiteSource",
                table: "Companies",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CapturedJobDescriptionHtml",
                table: "Applications",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CompanyProfileSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyProfileSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyProfileSubmissions_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompanyProfileSubmissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyProfileSubmissions_CompanyId_Platform_UserId",
                table: "CompanyProfileSubmissions",
                columns: new[] { "CompanyId", "Platform", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyProfileSubmissions_UserId",
                table: "CompanyProfileSubmissions",
                column: "UserId");

            // A captured description used to be written onto the shared Job row, where everyone who
            // later captured the same posting read it. It now lives on each application. So that
            // nobody loses the description they see today, the Job's text is copied onto every
            // application of that job, and only then does it leave the shared row; from here on
            // each capture keeps its own, and the Job's own description comes only from the
            // server-side ATS read, which refills it on the posting's next capture.
            migrationBuilder.Sql("""
                UPDATE "Applications" a
                SET "CapturedJobDescriptionHtml" = j."DescriptionHtml"
                FROM "Jobs" j
                WHERE a."JobId" = j."Id"
                  AND j."DescriptionHtml" IS NOT NULL
                  AND a."CapturedJobDescriptionHtml" IS NULL;

                UPDATE "Jobs" SET "Description" = NULL, "DescriptionHtml" = NULL
                WHERE "Description" IS NOT NULL OR "DescriptionHtml" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyProfileSubmissions");

            migrationBuilder.DropColumn(
                name: "WebsiteSource",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "CapturedJobDescriptionHtml",
                table: "Applications");
        }
    }
}
