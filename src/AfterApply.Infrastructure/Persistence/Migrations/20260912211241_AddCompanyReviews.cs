using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReviewQuotaOverride",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Companies",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CompanyReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmploymentStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Pros = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Cons = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    OverallRating = table.Column<int>(type: "integer", nullable: false),
                    ManagementRating = table.Column<int>(type: "integer", nullable: false),
                    WorkEnvironmentRating = table.Column<int>(type: "integer", nullable: false),
                    SalaryAndBenefitsRating = table.Column<int>(type: "integer", nullable: false),
                    CareerAndDevelopmentRating = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModeratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModeratedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyReviews_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyReviews_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompanyReviewHelpfulMarks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MarkedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyReviewHelpfulMarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyReviewHelpfulMarks_CompanyReviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "CompanyReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompanyReviewHelpfulMarks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompanyReviewReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Resolution = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ResolutionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyReviewReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyReviewReports_CompanyReviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "CompanyReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompanyReviewReports_Users_ReporterUserId",
                        column: x => x.ReporterUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Every existing company gets its public-page slug here, in SQL, so the unique index
            // below can be created in the same migration. The Turkish fold table mirrors
            // CompanySlugGenerator (a unit test pins both to the same inputs): İ/I/ı fold to "i"
            // *before* lower(), because lower() alone leaves the dotted/dotless pair apart. Names
            // that collapse to the same slug — or to a segment the web app reserves under
            // /companies — get "-2", "-3", … in creation order, which is what the allocator does
            // for new rows. Nothing here rewrites a slug that is already set (WHERE "Slug" IS NULL),
            // so re-running is safe and a rollout's late inserts are picked up by the service.
            migrationBuilder.Sql("""
                WITH base AS (
                    SELECT "Id", "CreatedAt",
                           trim(both '-' from regexp_replace(
                               lower(translate("Name", 'İIıÇĞÖŞÜçğöşüÂÎÛâîû', 'iiiCGOSUcgosuAIUaiu')),
                               '[^a-z0-9]+', '-', 'g')) AS s
                    FROM "Companies"
                    WHERE "Slug" IS NULL),
                ranked AS (
                    SELECT "Id",
                           CASE WHEN s = '' THEN 'company' ELSE left(s, 80) END AS s,
                           row_number() OVER (
                               PARTITION BY CASE WHEN s = '' THEN 'company' ELSE left(s, 80) END
                               ORDER BY "CreatedAt", "Id") AS rn
                    FROM base)
                UPDATE "Companies" c
                   SET "Slug" = CASE WHEN ranked.s IN ('scoring', 'search', 'public', 'new') THEN ranked.s || '-' || (ranked.rn + 1)
                                     WHEN ranked.rn = 1 THEN ranked.s
                                     ELSE ranked.s || '-' || ranked.rn END
                  FROM ranked
                 WHERE c."Id" = ranked."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Slug",
                table: "Companies",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyReviewHelpfulMarks_ReviewId_UserId",
                table: "CompanyReviewHelpfulMarks",
                columns: new[] { "ReviewId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyReviewHelpfulMarks_UserId",
                table: "CompanyReviewHelpfulMarks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyReviewReports_ReporterUserId",
                table: "CompanyReviewReports",
                column: "ReporterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyReviewReports_ReviewId_ReporterUserId",
                table: "CompanyReviewReports",
                columns: new[] { "ReviewId", "ReporterUserId" },
                unique: true,
                filter: "\"Status\" = 'Open'");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyReviewReports_Status_ReportedAt",
                table: "CompanyReviewReports",
                columns: new[] { "Status", "ReportedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyReviews_CompanyId_Status_SubmittedAt",
                table: "CompanyReviews",
                columns: new[] { "CompanyId", "Status", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyReviews_Status_SubmittedAt",
                table: "CompanyReviews",
                columns: new[] { "Status", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyReviews_UserId_CompanyId",
                table: "CompanyReviews",
                columns: new[] { "UserId", "CompanyId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyReviewHelpfulMarks");

            migrationBuilder.DropTable(
                name: "CompanyReviewReports");

            migrationBuilder.DropTable(
                name: "CompanyReviews");

            migrationBuilder.DropIndex(
                name: "IX_Companies_Slug",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ReviewQuotaOverride",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Companies");
        }
    }
}
