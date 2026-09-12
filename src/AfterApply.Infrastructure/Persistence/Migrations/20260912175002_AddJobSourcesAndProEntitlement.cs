using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobSourcesAndProEntitlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobSourceFetches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: true),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobSourceFetches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobSourcePostings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CompanyProfileUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PostedAt = table.Column<DateOnly>(type: "date", nullable: true),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Seniority = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EmploymentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    JobFunction = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Industries = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DetailFetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobSourcePostings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobSourceQueries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Keywords = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TimeWindow = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RemoteOnly = table.Column<bool>(type: "boolean", nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastResultCount = table.Column<int>(type: "integer", nullable: true),
                    LastOutcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobSourceQueries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProEntitlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActiveUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProEntitlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProEntitlements_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserJobSourceProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RemoteOnly = table.Column<bool>(type: "boolean", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserJobSourceProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserJobSourceProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserJobSourceRuns",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WeekKey = table.Column<int>(type: "integer", nullable: false),
                    RanAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CandidateCount = table.Column<int>(type: "integer", nullable: false),
                    DeliveredCount = table.Column<int>(type: "integer", nullable: false),
                    ExcludedAppliedCount = table.Column<int>(type: "integer", nullable: false),
                    ExcludedRecentlyShownCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserJobSourceRuns", x => new { x.UserId, x.WeekKey });
                    table.ForeignKey(
                        name: "FK_UserJobSourceRuns_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserJobSourceSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WeeklyPostingLimit = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserJobSourceSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserJobSourceSettings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobSourceQueryPostings",
                columns: table => new
                {
                    QueryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostingId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobSourceQueryPostings", x => new { x.QueryId, x.PostingId });
                    table.ForeignKey(
                        name: "FK_JobSourceQueryPostings_JobSourcePostings_PostingId",
                        column: x => x.PostingId,
                        principalTable: "JobSourcePostings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobSourceQueryPostings_JobSourceQueries_QueryId",
                        column: x => x.QueryId,
                        principalTable: "JobSourceQueries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserJobSourceDeliveries",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostingId = table.Column<Guid>(type: "uuid", nullable: false),
                    QueryId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    WeekKey = table.Column<int>(type: "integer", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserJobSourceDeliveries", x => new { x.UserId, x.PostingId });
                    table.ForeignKey(
                        name: "FK_UserJobSourceDeliveries_JobSourcePostings_PostingId",
                        column: x => x.PostingId,
                        principalTable: "JobSourcePostings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserJobSourceDeliveries_JobSourceQueries_QueryId",
                        column: x => x.QueryId,
                        principalTable: "JobSourceQueries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserJobSourceDeliveries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserJobSourceProfileQueries",
                columns: table => new
                {
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    QueryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserJobSourceProfileQueries", x => new { x.ProfileId, x.QueryId });
                    table.ForeignKey(
                        name: "FK_UserJobSourceProfileQueries_JobSourceQueries_QueryId",
                        column: x => x.QueryId,
                        principalTable: "JobSourceQueries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserJobSourceProfileQueries_UserJobSourceProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "UserJobSourceProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobSourceFetches_At",
                table: "JobSourceFetches",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_JobSourcePostings_LastSeenAt",
                table: "JobSourcePostings",
                column: "LastSeenAt");

            migrationBuilder.CreateIndex(
                name: "IX_JobSourcePostings_Source_ExternalId",
                table: "JobSourcePostings",
                columns: new[] { "Source", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobSourceQueries_KeyHash",
                table: "JobSourceQueries",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobSourceQueryPostings_PostingId",
                table: "JobSourceQueryPostings",
                column: "PostingId");

            migrationBuilder.CreateIndex(
                name: "IX_JobSourceQueryPostings_QueryId_LastSeenAt",
                table: "JobSourceQueryPostings",
                columns: new[] { "QueryId", "LastSeenAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProEntitlements_UserId",
                table: "ProEntitlements",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserJobSourceDeliveries_PostingId",
                table: "UserJobSourceDeliveries",
                column: "PostingId");

            migrationBuilder.CreateIndex(
                name: "IX_UserJobSourceDeliveries_QueryId",
                table: "UserJobSourceDeliveries",
                column: "QueryId");

            migrationBuilder.CreateIndex(
                name: "IX_UserJobSourceDeliveries_UserId_DeliveredAt",
                table: "UserJobSourceDeliveries",
                columns: new[] { "UserId", "DeliveredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserJobSourceDeliveries_UserId_WeekKey_Rank",
                table: "UserJobSourceDeliveries",
                columns: new[] { "UserId", "WeekKey", "Rank" });

            migrationBuilder.CreateIndex(
                name: "IX_UserJobSourceProfileQueries_QueryId",
                table: "UserJobSourceProfileQueries",
                column: "QueryId");

            migrationBuilder.CreateIndex(
                name: "IX_UserJobSourceProfiles_UserId",
                table: "UserJobSourceProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserJobSourceSettings_UserId",
                table: "UserJobSourceSettings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobSourceFetches");

            migrationBuilder.DropTable(
                name: "JobSourceQueryPostings");

            migrationBuilder.DropTable(
                name: "ProEntitlements");

            migrationBuilder.DropTable(
                name: "UserJobSourceDeliveries");

            migrationBuilder.DropTable(
                name: "UserJobSourceProfileQueries");

            migrationBuilder.DropTable(
                name: "UserJobSourceRuns");

            migrationBuilder.DropTable(
                name: "UserJobSourceSettings");

            migrationBuilder.DropTable(
                name: "JobSourcePostings");

            migrationBuilder.DropTable(
                name: "JobSourceQueries");

            migrationBuilder.DropTable(
                name: "UserJobSourceProfiles");
        }
    }
}
