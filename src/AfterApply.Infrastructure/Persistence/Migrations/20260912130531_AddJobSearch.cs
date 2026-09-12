using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobSearchCacheEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Parameters = table.Column<string>(type: "text", nullable: true),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    UpstreamRequestId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobSearchCacheEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobSearchJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    EmployerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Publisher = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Summary = table.Column<string>(type: "jsonb", nullable: false),
                    Detail = table.Column<string>(type: "jsonb", nullable: true),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    SummaryFetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DetailFetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DetailExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobSearchJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobSearchUsages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Credits = table.Column<int>(type: "integer", nullable: false),
                    CacheHit = table.Column<bool>(type: "boolean", nullable: false),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: true),
                    UpstreamRequestId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpstreamRequestsRemaining = table.Column<int>(type: "integer", nullable: true),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobSearchUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobSearchUsages_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobSearchUserSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultCountry = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    DefaultLanguage = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    DefaultLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DefaultDatePosted = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    DefaultWorkFromHome = table.Column<bool>(type: "boolean", nullable: true),
                    PerUserDailyCredits = table.Column<int>(type: "integer", nullable: true),
                    MaxPagesPerSearch = table.Column<int>(type: "integer", nullable: true),
                    MaxJobIdsPerDetails = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobSearchUserSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobSearchUserSettings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobSearchCacheEntries_ExpiresAt",
                table: "JobSearchCacheEntries",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_JobSearchCacheEntries_Operation_KeyHash",
                table: "JobSearchCacheEntries",
                columns: new[] { "Operation", "KeyHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobSearchJobs_EmployerName",
                table: "JobSearchJobs",
                column: "EmployerName");

            migrationBuilder.CreateIndex(
                name: "IX_JobSearchJobs_JobId_Country",
                table: "JobSearchJobs",
                columns: new[] { "JobId", "Country" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobSearchUsages_RequestedAt",
                table: "JobSearchUsages",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_JobSearchUsages_UserId_RequestedAt",
                table: "JobSearchUsages",
                columns: new[] { "UserId", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_JobSearchUserSettings_UserId",
                table: "JobSearchUserSettings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobSearchCacheEntries");

            migrationBuilder.DropTable(
                name: "JobSearchJobs");

            migrationBuilder.DropTable(
                name: "JobSearchUsages");

            migrationBuilder.DropTable(
                name: "JobSearchUserSettings");
        }
    }
}
