using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReplyPromiseAndRejectionNotice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "PromisedReplyBy",
                table: "Applications",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PromisedReplySince",
                table: "Applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromisedReplyStatus",
                table: "Applications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionNotice",
                table: "Applications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // The one answer the history already holds: a rejection applied from the company's own
            // email (confirmed or auto-applied) was, by definition, told to the candidate — the same
            // rule Application.ChangeStatus applies from now on. Only when that email row is the
            // application's latest transition, i.e. the rejection is still the current one. Every
            // other rejection stays null: we don't know, and the rate counts only known answers.
            migrationBuilder.Sql("""
                UPDATE "Applications" AS a
                SET "RejectionNotice" = 'CompanyNotified'
                WHERE a."Status" = 'Rejected'
                  AND EXISTS (
                      SELECT 1 FROM "ApplicationStatusHistories" AS h
                      WHERE h."ApplicationId" = a."Id"
                        AND h."ToStatus" = 'Rejected'
                        AND h."Origin" IN ('EmailSuggestionConfirmed', 'EmailAutoApplied')
                        AND h."ChangedAt" = (
                            SELECT max(h2."ChangedAt") FROM "ApplicationStatusHistories" AS h2
                            WHERE h2."ApplicationId" = a."Id"));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PromisedReplyBy",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "PromisedReplySince",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "PromisedReplyStatus",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "RejectionNotice",
                table: "Applications");
        }
    }
}
