using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEmailForwarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The forward-all-inbox-to-us design was removed from the app entirely (see
            // DECISIONS.md); any Forwarding-provider connections (and their EmailSuggestions, via
            // FK cascade) are now unreachable dead data — same pattern as RemoveGmailIntegration's
            // 'Gmail' cleanup.
            migrationBuilder.Sql("""DELETE FROM "EmailConnections" WHERE "Provider" = 'Forwarding';""");

            migrationBuilder.DropIndex(
                name: "IX_EmailConnections_InboundToken",
                table: "EmailConnections");

            migrationBuilder.DropColumn(
                name: "GmailConfirmationCode",
                table: "EmailConnections");

            migrationBuilder.DropColumn(
                name: "GmailConfirmationLink",
                table: "EmailConnections");

            migrationBuilder.DropColumn(
                name: "GmailConfirmationReceivedAt",
                table: "EmailConnections");

            migrationBuilder.DropColumn(
                name: "InboundToken",
                table: "EmailConnections");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GmailConfirmationCode",
                table: "EmailConnections",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GmailConfirmationLink",
                table: "EmailConnections",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GmailConfirmationReceivedAt",
                table: "EmailConnections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InboundToken",
                table: "EmailConnections",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailConnections_InboundToken",
                table: "EmailConnections",
                column: "InboundToken",
                unique: true);
        }
    }
}
