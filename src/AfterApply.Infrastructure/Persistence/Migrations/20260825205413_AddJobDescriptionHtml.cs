using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobDescriptionHtml : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DescriptionHtml",
                table: "Jobs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DescriptionHtml",
                table: "Jobs");
        }
    }
}
