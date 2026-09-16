using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobFitScoringAndAiUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AiScoringConsentAcceptedAt",
                table: "UserJobSourceProfiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinScore",
                table: "UserJobSourceProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string[]>(
                name: "MatchedCriteria",
                table: "UserJobSourceDeliveries",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string[]>(
                name: "MissingCriteria",
                table: "UserJobSourceDeliveries",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string[]>(
                name: "RequiredSkills",
                table: "UserJobSourceDeliveries",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<int>(
                name: "Score",
                table: "UserJobSourceDeliveries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScoreAttempts",
                table: "UserJobSourceDeliveries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ScoreSummary",
                table: "UserJobSourceDeliveries",
                type: "character varying(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ScoredAt",
                table: "UserJobSourceDeliveries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AiUsageEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Feature = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUsageEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiUsageEntries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageEntries_Feature_At",
                table: "AiUsageEntries",
                columns: new[] { "Feature", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageEntries_UserId",
                table: "AiUsageEntries",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiUsageEntries");

            migrationBuilder.DropColumn(
                name: "AiScoringConsentAcceptedAt",
                table: "UserJobSourceProfiles");

            migrationBuilder.DropColumn(
                name: "MinScore",
                table: "UserJobSourceProfiles");

            migrationBuilder.DropColumn(
                name: "MatchedCriteria",
                table: "UserJobSourceDeliveries");

            migrationBuilder.DropColumn(
                name: "MissingCriteria",
                table: "UserJobSourceDeliveries");

            migrationBuilder.DropColumn(
                name: "RequiredSkills",
                table: "UserJobSourceDeliveries");

            migrationBuilder.DropColumn(
                name: "Score",
                table: "UserJobSourceDeliveries");

            migrationBuilder.DropColumn(
                name: "ScoreAttempts",
                table: "UserJobSourceDeliveries");

            migrationBuilder.DropColumn(
                name: "ScoreSummary",
                table: "UserJobSourceDeliveries");

            migrationBuilder.DropColumn(
                name: "ScoredAt",
                table: "UserJobSourceDeliveries");
        }
    }
}
