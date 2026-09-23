using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContributionNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing accounts start with every notification on, like a new one (ApplicationUser's
            // initialisers). Hand-edited from the scaffold's false; the model snapshot keeps no
            // database default on purpose — a false written by EF must stay false.
            migrationBuilder.AddColumn<bool>(
                name: "NotifyBlogCommentHelpful",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyContributions",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyExperienceHelpful",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyGmailUpdates",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyReviewHelpful",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifySalaryHelpful",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "CandidateExperienceHelpfulMarks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExperienceId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MarkedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateExperienceHelpfulMarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateExperienceHelpfulMarks_CandidateExperiences_Experi~",
                        column: x => x.ExperienceId,
                        principalTable: "CandidateExperiences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CandidateExperienceHelpfulMarks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompanySalaryHelpfulMarks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MarkedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanySalaryHelpfulMarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanySalaryHelpfulMarks_CompanySalaryEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "CompanySalaryEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompanySalaryHelpfulMarks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContributionNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    LastEventAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DismissedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContributionNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContributionNotifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HelpfulNotificationLedger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpfulNotificationLedger", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HelpfulNotificationLedger_Users_VoterUserId",
                        column: x => x.VoterUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateExperienceHelpfulMarks_ExperienceId_UserId",
                table: "CandidateExperienceHelpfulMarks",
                columns: new[] { "ExperienceId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CandidateExperienceHelpfulMarks_UserId",
                table: "CandidateExperienceHelpfulMarks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanySalaryHelpfulMarks_EntryId_UserId",
                table: "CompanySalaryHelpfulMarks",
                columns: new[] { "EntryId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanySalaryHelpfulMarks_UserId",
                table: "CompanySalaryHelpfulMarks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContributionNotifications_UserId_LastEventAt",
                table: "ContributionNotifications",
                columns: new[] { "UserId", "LastEventAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ContributionNotifications_UserId_Type_TargetId_Day",
                table: "ContributionNotifications",
                columns: new[] { "UserId", "Type", "TargetId", "Day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HelpfulNotificationLedger_CountedAt",
                table: "HelpfulNotificationLedger",
                column: "CountedAt");

            migrationBuilder.CreateIndex(
                name: "IX_HelpfulNotificationLedger_Type_TargetId_VoterUserId",
                table: "HelpfulNotificationLedger",
                columns: new[] { "Type", "TargetId", "VoterUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HelpfulNotificationLedger_VoterUserId",
                table: "HelpfulNotificationLedger",
                column: "VoterUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CandidateExperienceHelpfulMarks");

            migrationBuilder.DropTable(
                name: "CompanySalaryHelpfulMarks");

            migrationBuilder.DropTable(
                name: "ContributionNotifications");

            migrationBuilder.DropTable(
                name: "HelpfulNotificationLedger");

            migrationBuilder.DropColumn(
                name: "NotifyBlogCommentHelpful",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NotifyContributions",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NotifyExperienceHelpful",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NotifyGmailUpdates",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NotifyReviewHelpful",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NotifySalaryHelpful",
                table: "Users");
        }
    }
}
