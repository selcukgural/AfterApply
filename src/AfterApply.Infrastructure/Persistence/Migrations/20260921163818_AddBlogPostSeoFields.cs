using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBlogPostSeoFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverAlt",
                table: "BlogPosts",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DraftCoverAlt",
                table: "BlogPosts",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DraftPrimaryKeyword",
                table: "BlogPosts",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "DraftSecondaryKeywords",
                table: "BlogPosts",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string>(
                name: "DraftSeoTitle",
                table: "BlogPosts",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryKeyword",
                table: "BlogPosts",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "SecondaryKeywords",
                table: "BlogPosts",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string>(
                name: "SeoTitle",
                table: "BlogPosts",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverAlt",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "DraftCoverAlt",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "DraftPrimaryKeyword",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "DraftSecondaryKeywords",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "DraftSeoTitle",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "PrimaryKeyword",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "SecondaryKeywords",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "SeoTitle",
                table: "BlogPosts");
        }
    }
}
