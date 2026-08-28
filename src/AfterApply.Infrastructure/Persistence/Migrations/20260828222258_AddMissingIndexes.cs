using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_EmailSuggestions_UserId_Status",
                table: "EmailSuggestions",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_UserId_Status",
                table: "Applications",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailSuggestions_UserId_Status",
                table: "EmailSuggestions");

            migrationBuilder.DropIndex(
                name: "IX_Applications_UserId_Status",
                table: "Applications");
        }
    }
}
