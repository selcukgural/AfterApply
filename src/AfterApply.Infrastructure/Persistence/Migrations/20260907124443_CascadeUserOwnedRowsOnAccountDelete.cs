using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CascadeUserOwnedRowsOnAccountDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Adding the foreign keys validates the existing rows, so anything already orphaned has
            // to go first or the migration itself fails on deploy. These rows are exactly the ones
            // that should not exist: they belong to accounts that were already deleted and are
            // unreachable through every endpoint (each one filters on the caller's own user id).
            // The hand-written sweep in DeleteAccountAsync never covered TrackedJobs, Reminders or
            // EmailSuggestions, so any account deleted before today left its rows behind.
            //
            // Applications go first: their cascade takes ApplicationEvents, ApplicationStatusHistories
            // and any Reminders/EmailSuggestions hanging off them, so the later statements only have
            // to sweep what was orphaned on its own.
            //
            // One caveat: an orphaned CvDocuments row's file in Cloud Storage cannot be removed from
            // a migration, so a stray object could survive its row. In practice there are none —
            // CV upload shipped 2026-09-07 with storage deletion in DeleteAccountAsync from the
            // first commit — but if the count below is ever non-zero, the bucket needs a look.
            migrationBuilder.Sql("""
                DELETE FROM "Applications" a WHERE NOT EXISTS (SELECT 1 FROM "Users" u WHERE u."Id" = a."UserId");
                DELETE FROM "CvDocuments" c WHERE NOT EXISTS (SELECT 1 FROM "Users" u WHERE u."Id" = c."UserId");
                DELETE FROM "ImportBatches" b WHERE NOT EXISTS (SELECT 1 FROM "Users" u WHERE u."Id" = b."UserId");
                DELETE FROM "Reminders" r WHERE NOT EXISTS (SELECT 1 FROM "Users" u WHERE u."Id" = r."UserId");
                DELETE FROM "EmailSuggestions" s WHERE NOT EXISTS (SELECT 1 FROM "Users" u WHERE u."Id" = s."UserId");
                DELETE FROM "TrackedJobs" t WHERE NOT EXISTS (SELECT 1 FROM "Users" u WHERE u."Id" = t."UserId");
                DELETE FROM "FeedbackEntries" f WHERE NOT EXISTS (SELECT 1 FROM "Users" u WHERE u."Id" = f."UserId");
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_Users_UserId",
                table: "Applications",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CvDocuments_Users_UserId",
                table: "CvDocuments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmailSuggestions_Users_UserId",
                table: "EmailSuggestions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FeedbackEntries_Users_UserId",
                table: "FeedbackEntries",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ImportBatches_Users_UserId",
                table: "ImportBatches",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Reminders_Users_UserId",
                table: "Reminders",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TrackedJobs_Users_UserId",
                table: "TrackedJobs",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        // Drops the constraints only. The orphaned rows Up deleted are not coming back, which is
        // the intended outcome — they were data belonging to accounts that had asked to be deleted.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Applications_Users_UserId",
                table: "Applications");

            migrationBuilder.DropForeignKey(
                name: "FK_CvDocuments_Users_UserId",
                table: "CvDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_EmailSuggestions_Users_UserId",
                table: "EmailSuggestions");

            migrationBuilder.DropForeignKey(
                name: "FK_FeedbackEntries_Users_UserId",
                table: "FeedbackEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_ImportBatches_Users_UserId",
                table: "ImportBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_Reminders_Users_UserId",
                table: "Reminders");

            migrationBuilder.DropForeignKey(
                name: "FK_TrackedJobs_Users_UserId",
                table: "TrackedJobs");
        }
    }
}
