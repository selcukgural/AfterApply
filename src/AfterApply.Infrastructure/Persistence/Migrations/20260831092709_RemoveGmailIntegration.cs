using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveGmailIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Gmail OAuth support was removed from the app entirely (see DECISIONS.md); any
            // Gmail-provider connections (and their EmailSuggestions, via FK cascade) are gone too —
            // the app never launched, so this can't touch real user data.
            migrationBuilder.Sql("""DELETE FROM "EmailConnections" WHERE "Provider" = 'Gmail';""");

            migrationBuilder.DropColumn(
                name: "DisconnectedAt",
                table: "EmailConnections");

            migrationBuilder.DropColumn(
                name: "EncryptedRefreshToken",
                table: "EmailConnections");

            migrationBuilder.DropColumn(
                name: "GrantedScopes",
                table: "EmailConnections");

            migrationBuilder.DropColumn(
                name: "LastSyncError",
                table: "EmailConnections");

            migrationBuilder.DropColumn(
                name: "LastSyncErrorAt",
                table: "EmailConnections");

            migrationBuilder.DropColumn(
                name: "LastSyncedAt",
                table: "EmailConnections");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DisconnectedAt",
                table: "EmailConnections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncryptedRefreshToken",
                table: "EmailConnections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GrantedScopes",
                table: "EmailConnections",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastSyncError",
                table: "EmailConnections",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSyncErrorAt",
                table: "EmailConnections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSyncedAt",
                table: "EmailConnections",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
