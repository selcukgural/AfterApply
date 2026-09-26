using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBlogPostKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BlogPosts_Language_Slug",
                table: "BlogPosts");

            migrationBuilder.DropIndex(
                name: "IX_BlogPosts_Status_Language_PublishedAt",
                table: "BlogPosts");

            migrationBuilder.AddColumn<bool>(
                name: "DraftHideRegisterCta",
                table: "BlogPosts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid[]>(
                name: "DraftRelatedPostIds",
                table: "BlogPosts",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);

            migrationBuilder.AddColumn<bool>(
                name: "HideRegisterCta",
                table: "BlogPosts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "BlogPosts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Blog");

            migrationBuilder.AddColumn<Guid[]>(
                name: "RelatedPostIds",
                table: "BlogPosts",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_Kind_Language_Slug",
                table: "BlogPosts",
                columns: new[] { "Kind", "Language", "Slug" },
                unique: true,
                filter: "\"Slug\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_Kind_Status_Language_PublishedAt",
                table: "BlogPosts",
                columns: new[] { "Kind", "Status", "Language", "PublishedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BlogPosts_Kind_Language_Slug",
                table: "BlogPosts");

            migrationBuilder.DropIndex(
                name: "IX_BlogPosts_Kind_Status_Language_PublishedAt",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "DraftHideRegisterCta",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "DraftRelatedPostIds",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "HideRegisterCta",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "RelatedPostIds",
                table: "BlogPosts");

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_Language_Slug",
                table: "BlogPosts",
                columns: new[] { "Language", "Slug" },
                unique: true,
                filter: "\"Slug\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_Status_Language_PublishedAt",
                table: "BlogPosts",
                columns: new[] { "Status", "Language", "PublishedAt" });
        }
    }
}
