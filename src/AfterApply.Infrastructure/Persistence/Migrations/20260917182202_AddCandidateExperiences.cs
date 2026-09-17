using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateExperiences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CandidateExperiences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OverallRating = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Duration = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Stages = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateExperiences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateExperiences_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CandidateExperiences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CandidateExperienceCategoryRatings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExperienceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateExperienceCategoryRatings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateExperienceCategoryRatings_CandidateExperiences_Exp~",
                        column: x => x.ExperienceId,
                        principalTable: "CandidateExperiences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CandidateExperienceInterviewTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExperienceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateExperienceInterviewTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateExperienceInterviewTypes_CandidateExperiences_Expe~",
                        column: x => x.ExperienceId,
                        principalTable: "CandidateExperiences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CandidateExperienceStatementPicks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExperienceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StatementKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateExperienceStatementPicks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateExperienceStatementPicks_CandidateExperiences_Expe~",
                        column: x => x.ExperienceId,
                        principalTable: "CandidateExperiences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateExperienceCategoryRatings_ExperienceId_Category",
                table: "CandidateExperienceCategoryRatings",
                columns: new[] { "ExperienceId", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CandidateExperienceInterviewTypes_ExperienceId_Type",
                table: "CandidateExperienceInterviewTypes",
                columns: new[] { "ExperienceId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CandidateExperienceInterviewTypes_Type",
                table: "CandidateExperienceInterviewTypes",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateExperiences_CompanyId_SubmittedAt",
                table: "CandidateExperiences",
                columns: new[] { "CompanyId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateExperiences_UserId",
                table: "CandidateExperiences",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateExperiences_UserId_CompanyId",
                table: "CandidateExperiences",
                columns: new[] { "UserId", "CompanyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CandidateExperienceStatementPicks_ExperienceId_StatementKey",
                table: "CandidateExperienceStatementPicks",
                columns: new[] { "ExperienceId", "StatementKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CandidateExperienceStatementPicks_StatementKey",
                table: "CandidateExperienceStatementPicks",
                column: "StatementKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CandidateExperienceCategoryRatings");

            migrationBuilder.DropTable(
                name: "CandidateExperienceInterviewTypes");

            migrationBuilder.DropTable(
                name: "CandidateExperienceStatementPicks");

            migrationBuilder.DropTable(
                name: "CandidateExperiences");
        }
    }
}
