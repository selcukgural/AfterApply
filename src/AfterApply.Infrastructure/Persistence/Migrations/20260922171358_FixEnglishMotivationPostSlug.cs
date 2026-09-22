using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Corrects a typo ("kep") in the slug of the English motivation post, which the publish
    /// generated from a draft title that had it. A published slug is locked (DECISIONS.md
    /// 2026-09-19), so the rename happens here and nowhere else; the old address keeps working
    /// through the 301 in <c>web/src/lib/blog/slugRedirects.ts</c>, which ships in the same change.
    /// Data only — no schema change. A no-op where the post does not exist (local, tests), and it
    /// leaves the row alone if something already holds the corrected slug rather than tripping
    /// the unique index.
    /// </summary>
    public partial class FixEnglishMotivationPostSlug : Migration
    {
        private const string OldSlug = "how-can-i-kep-my-motivation-while-job-searching";
        private const string NewSlug = "how-can-i-keep-my-motivation-while-job-searching";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(RenameSql(OldSlug, NewSlug));

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(RenameSql(NewSlug, OldSlug));

        private static string RenameSql(string from, string to) => $"""
            UPDATE "BlogPosts"
            SET "Slug" = '{to}'
            WHERE "Language" = 'en' AND "Slug" = '{from}'
              AND NOT EXISTS (SELECT 1 FROM "BlogPosts" WHERE "Language" = 'en' AND "Slug" = '{to}');
            """;
    }
}
