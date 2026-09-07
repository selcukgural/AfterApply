using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedbackEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeedbackEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Mood = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ReplyEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    PagePath = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Locale = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Theme = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AdminReply = table.Column<string>(type: "text", nullable: true),
                    AdminReplyAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GitHubIssueNumber = table.Column<int>(type: "integer", nullable: true),
                    GitHubIssueUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MirroredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackEntries_UserId_SubmittedAt",
                table: "FeedbackEntries",
                columns: new[] { "UserId", "SubmittedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeedbackEntries");
        }
    }
}
