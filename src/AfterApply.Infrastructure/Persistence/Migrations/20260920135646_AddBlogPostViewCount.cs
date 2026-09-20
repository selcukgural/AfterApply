using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBlogPostViewCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ViewCount",
                table: "BlogPosts",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ViewCount",
                table: "BlogPosts");
        }
    }
}
