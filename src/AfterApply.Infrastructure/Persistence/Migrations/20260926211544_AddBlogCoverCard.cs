using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBlogCoverCard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverHook",
                table: "BlogPosts",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoverIcon",
                table: "BlogPosts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DraftCoverHook",
                table: "BlogPosts",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DraftCoverIcon",
                table: "BlogPosts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverHook",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "CoverIcon",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "DraftCoverHook",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "DraftCoverIcon",
                table: "BlogPosts");
        }
    }
}
