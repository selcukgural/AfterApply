using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailForwarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Snippet",
                table: "EmailSuggestions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "EmailSuggestions",
                type: "character varying(500)",
                maxLength: 500,
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailConnections_InboundToken",
                table: "EmailConnections");

            migrationBuilder.DropColumn(
                name: "Snippet",
                table: "EmailSuggestions");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "EmailSuggestions");

            migrationBuilder.DropColumn(
                name: "InboundToken",
                table: "EmailConnections");
        }
    }
}
