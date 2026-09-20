using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBlogComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BlogComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentCommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Content = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EditedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModeratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModeratedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlogComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BlogComments_BlogComments_ParentCommentId",
                        column: x => x.ParentCommentId,
                        principalTable: "BlogComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BlogComments_BlogPosts_PostId",
                        column: x => x.PostId,
                        principalTable: "BlogPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BlogComments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BlogCommentHelpfulVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    VotedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlogCommentHelpfulVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BlogCommentHelpfulVotes_BlogComments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "BlogComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BlogCommentHelpfulVotes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BlogCommentReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlogCommentReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BlogCommentReports_BlogComments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "BlogComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BlogCommentReports_Users_ReporterUserId",
                        column: x => x.ReporterUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlogCommentHelpfulVotes_CommentId_UserId",
                table: "BlogCommentHelpfulVotes",
                columns: new[] { "CommentId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlogCommentHelpfulVotes_UserId",
                table: "BlogCommentHelpfulVotes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BlogCommentReports_CommentId_ReporterUserId",
                table: "BlogCommentReports",
                columns: new[] { "CommentId", "ReporterUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlogCommentReports_CommentId_Status",
                table: "BlogCommentReports",
                columns: new[] { "CommentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BlogCommentReports_ReporterUserId",
                table: "BlogCommentReports",
                column: "ReporterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BlogComments_ParentCommentId",
                table: "BlogComments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_BlogComments_PostId_Status_CreatedAt",
                table: "BlogComments",
                columns: new[] { "PostId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BlogComments_UserId_CreatedAt",
                table: "BlogComments",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlogCommentHelpfulVotes");

            migrationBuilder.DropTable(
                name: "BlogCommentReports");

            migrationBuilder.DropTable(
                name: "BlogComments");
        }
    }
}
