using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusChangeOrigin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EmailSuggestionId",
                table: "ApplicationStatusHistories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Origin",
                table: "ApplicationStatusHistories",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RejectionReasonCategory",
                table: "ApplicationStatusHistories",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReasonDetail",
                table: "ApplicationStatusHistories",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "ApplicationStatusHistories",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationStatusHistories_ApplicationId_ChangedAt",
                table: "ApplicationStatusHistories",
                columns: new[] { "ApplicationId", "ChangedAt" });

            // Backfill, rather than leaving every pre-existing row's provenance permanently
            // "unknown". Enums are stored as text in this schema (HasConversion<string>), so these
            // compare against labels, not ordinals.
            //
            // Pass 1 — real transitions: the paired StatusChanged event already carries the Source.
            // It cannot distinguish a confirmed email suggestion from an auto-applied one (both are
            // Source.Email), so the Turkish sentence the old code wrote into Note is the only
            // evidence there is, and this is the first and last time it is useful. Notes themselves
            // are deliberately left untouched.
            migrationBuilder.Sql("""
                UPDATE "ApplicationStatusHistories" h
                SET "Source" = e."Source",
                    "Origin"  = CASE
                        WHEN h."Note" LIKE 'E-postadan otomatik uygulandı%' THEN 'EmailAutoApplied'
                        WHEN h."Note" LIKE 'E-postadan onaylandı%'
                          OR h."Note" LIKE 'E-postadan içe aktarıldı%'      THEN 'EmailSuggestionConfirmed'
                        WHEN e."Source" = 'Email'                           THEN 'EmailSuggestionConfirmed'
                        WHEN e."Source" IN ('CsvImport', 'LinkedInImport')  THEN 'Import'
                        WHEN e."Source" = 'BrowserExtension'                THEN 'Extension'
                        WHEN e."Source" = 'System'                          THEN 'System'
                        ELSE 'Manual'
                    END
                FROM "ApplicationEvents" e
                WHERE e."ApplicationId" = h."ApplicationId"
                  AND e."Type" = 'StatusChanged'
                  AND e."OccurredAt" = h."ChangedAt";
                """);

            // Pass 2 — the seed "→ Applied" row every application gets at creation. It has no
            // StatusChanged event to pair with, so its provenance comes from the application itself.
            migrationBuilder.Sql("""
                UPDATE "ApplicationStatusHistories" h
                SET "Source" = a."Source",
                    "Origin"  = CASE
                        WHEN a."Source" IN ('CsvImport', 'LinkedInImport') THEN 'Import'
                        WHEN a."Source" = 'BrowserExtension'               THEN 'Extension'
                        WHEN a."Source" = 'Email'                          THEN 'EmailSuggestionConfirmed'
                        WHEN a."Source" = 'System'                         THEN 'System'
                        ELSE 'Manual'
                    END
                FROM "Applications" a
                WHERE a."Id" = h."ApplicationId"
                  AND h."Source" = '';
                """);

            // Pass 3 — anything neither pass reached (a history row whose application or event row
            // is gone). Manual is the conservative label: it claims no automation happened.
            migrationBuilder.Sql("""
                DO $$
                DECLARE unmatched integer;
                BEGIN
                    SELECT count(*) INTO unmatched
                    FROM "ApplicationStatusHistories" WHERE "Origin" = '';

                    IF unmatched > 0 THEN
                        RAISE NOTICE 'AddStatusChangeOrigin: % status history row(s) had no source to backfill from; labelled Manual.', unmatched;
                        UPDATE "ApplicationStatusHistories"
                        SET "Origin" = 'Manual', "Source" = 'Manual'
                        WHERE "Origin" = '';
                    END IF;
                END $$;
                """);

            // The empty-string defaults exist only so the NOT NULL columns could be added to a
            // populated table. Every writer supplies both values, and the model declares no default
            // — drop them so the database matches.
            migrationBuilder.Sql("""
                ALTER TABLE "ApplicationStatusHistories" ALTER COLUMN "Origin" DROP DEFAULT;
                ALTER TABLE "ApplicationStatusHistories" ALTER COLUMN "Source" DROP DEFAULT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApplicationStatusHistories_ApplicationId_ChangedAt",
                table: "ApplicationStatusHistories");

            migrationBuilder.DropColumn(
                name: "EmailSuggestionId",
                table: "ApplicationStatusHistories");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "ApplicationStatusHistories");

            migrationBuilder.DropColumn(
                name: "RejectionReasonCategory",
                table: "ApplicationStatusHistories");

            migrationBuilder.DropColumn(
                name: "RejectionReasonDetail",
                table: "ApplicationStatusHistories");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "ApplicationStatusHistories");
        }
    }
}
