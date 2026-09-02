using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailSuggestionRejectionReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectionReasonCategory",
                table: "EmailSuggestions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "RejectionReasonConfidence",
                table: "EmailSuggestions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReasonDetail",
                table: "EmailSuggestions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RejectionReasonCategory",
                table: "EmailSuggestions");

            migrationBuilder.DropColumn(
                name: "RejectionReasonConfidence",
                table: "EmailSuggestions");

            migrationBuilder.DropColumn(
                name: "RejectionReasonDetail",
                table: "EmailSuggestions");
        }
    }
}
