using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailConnectionGmailConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GmailConfirmationCode",
                table: "EmailConnections");

            migrationBuilder.DropColumn(
                name: "GmailConfirmationLink",
                table: "EmailConnections");

            migrationBuilder.DropColumn(
                name: "GmailConfirmationReceivedAt",
                table: "EmailConnections");
        }
    }
}
