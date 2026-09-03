using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds Scope and ExpiresAt to PersonalAccessTokens (2026-09-03 security pass).
    ///
    /// Hand-written rather than left as scaffolded: the generated version added both columns
    /// NOT NULL with CLR defaults, which would have written 0001-01-01 into ExpiresAt (every
    /// already-issued token instantly expired, breaking every installed extension on deploy) and an
    /// empty string into Scope (not a valid PersonalAccessTokenScope, so EF would throw reading the
    /// row back). Add-nullable → backfill → set NOT NULL instead.
    ///
    /// Backfill choices: existing tokens become Full-scoped, because that is the access they were
    /// actually issued with and silently narrowing a live credential would break callers with no
    /// signal. They expire 90 days from *deploy* rather than 90 days from CreatedAt — an older
    /// token would otherwise be born already expired, which is the same outage the scaffolded
    /// default would have caused, just less obviously.
    /// </summary>
    public partial class AddPersonalAccessTokenScopeAndExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAt",
                table: "PersonalAccessTokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "PersonalAccessTokens",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "PersonalAccessTokens"
                SET "Scope" = 'Full',
                    "ExpiresAt" = now() + interval '90 days'
                WHERE "Scope" IS NULL;
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ExpiresAt",
                table: "PersonalAccessTokens",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Scope",
                table: "PersonalAccessTokens",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "PersonalAccessTokens");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "PersonalAccessTokens");
        }
    }
}
