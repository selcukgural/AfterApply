using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBlog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BlogMedia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploaderUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ObjectName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ByteSize = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlogMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BlogMedia_Users_UploaderUserId",
                        column: x => x.UploaderUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BlogPosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TranslationOfPostId = table.Column<Guid>(type: "uuid", nullable: true),
                    CoverMediaId = table.Column<Guid>(type: "uuid", nullable: true),
                    DraftTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DraftExcerpt = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DraftContentJson = table.Column<string>(type: "jsonb", nullable: false),
                    DraftContentHtml = table.Column<string>(type: "text", nullable: false),
                    DraftUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Excerpt = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentHtml = table.Column<string>(type: "text", nullable: false),
                    PublishedContentJson = table.Column<string>(type: "jsonb", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishedUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlogPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BlogPosts_BlogMedia_CoverMediaId",
                        column: x => x.CoverMediaId,
                        principalTable: "BlogMedia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BlogPosts_BlogPosts_TranslationOfPostId",
                        column: x => x.TranslationOfPostId,
                        principalTable: "BlogPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BlogPosts_Users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BlogPostLikes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LikedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlogPostLikes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BlogPostLikes_BlogPosts_PostId",
                        column: x => x.PostId,
                        principalTable: "BlogPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BlogPostLikes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlogMedia_PostId",
                table: "BlogMedia",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_BlogMedia_UploaderUserId",
                table: "BlogMedia",
                column: "UploaderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BlogPostLikes_PostId_UserId",
                table: "BlogPostLikes",
                columns: new[] { "PostId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlogPostLikes_UserId",
                table: "BlogPostLikes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_AuthorUserId",
                table: "BlogPosts",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_CoverMediaId",
                table: "BlogPosts",
                column: "CoverMediaId");

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

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_TranslationOfPostId",
                table: "BlogPosts",
                column: "TranslationOfPostId");

            migrationBuilder.AddForeignKey(
                name: "FK_BlogMedia_BlogPosts_PostId",
                table: "BlogMedia",
                column: "PostId",
                principalTable: "BlogPosts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BlogMedia_BlogPosts_PostId",
                table: "BlogMedia");

            migrationBuilder.DropTable(
                name: "BlogPostLikes");

            migrationBuilder.DropTable(
                name: "BlogPosts");

            migrationBuilder.DropTable(
                name: "BlogMedia");
        }
    }
}
