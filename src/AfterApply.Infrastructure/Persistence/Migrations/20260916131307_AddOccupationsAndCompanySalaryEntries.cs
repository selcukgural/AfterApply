using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOccupationsAndCompanySalaryEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Occupations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Isco08Code = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    NameTr = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedNameTr = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedNameEn = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Occupations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompanySalaryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccupationId = table.Column<Guid>(type: "uuid", nullable: false),
                    YearsOfExperience = table.Column<int>(type: "integer", nullable: false),
                    EmploymentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EmploymentStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    MonthlyNetAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    AnnualBonusAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanySalaryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanySalaryEntries_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanySalaryEntries_Occupations_OccupationId",
                        column: x => x.OccupationId,
                        principalTable: "Occupations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanySalaryEntries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanySalaryEntries_CompanyId_SubmittedAt",
                table: "CompanySalaryEntries",
                columns: new[] { "CompanyId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanySalaryEntries_OccupationId",
                table: "CompanySalaryEntries",
                column: "OccupationId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanySalaryEntries_UserId",
                table: "CompanySalaryEntries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanySalaryEntries_UserId_CompanyId_OccupationId",
                table: "CompanySalaryEntries",
                columns: new[] { "UserId", "CompanyId", "OccupationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Occupations_Code",
                table: "Occupations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Occupations_Isco08Code",
                table: "Occupations",
                column: "Isco08Code");

            // The typeahead's indexes — gin_trgm_ops has no fluent equivalent, so raw SQL as in
            // AddCompanyNameTrigramIndex (which already created the pg_trgm extension).
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_Occupations_NormalizedNameTr_Trgm\" " +
                "ON \"Occupations\" USING gin (\"NormalizedNameTr\" gin_trgm_ops);");
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_Occupations_NormalizedNameEn_Trgm\" " +
                "ON \"Occupations\" USING gin (\"NormalizedNameEn\" gin_trgm_ops);");

            // The catalogue itself: version 1 of the embedded CSV (436 ISCO-08 unit groups + the
            // curated market titles), upserted by code so the statement is the same on every
            // database this migration ever runs against. A later catalogue change is a v2 CSV and
            // its own migration — this one never changes.
            migrationBuilder.Sql(Occupations.OccupationSeedReader.BuildUpsertSql(1));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanySalaryEntries");

            migrationBuilder.DropTable(
                name: "Occupations");
        }
    }
}
