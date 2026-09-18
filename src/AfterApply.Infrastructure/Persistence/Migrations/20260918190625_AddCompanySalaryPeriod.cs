using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The period a salary was drawn in (2026-09-18). Both columns nullable: the rows that exist
    /// predate the question. A current employee's row is backfilled from its submission year —
    /// "still drawing it" is exactly what that row said — and a former employee's row is left
    /// null: only its author knows when it ended, and the first edit asks. Until then the page
    /// lists it under "previous periods" as "period not given" and the median skips it.
    /// </summary>
    public partial class AddCompanySalaryPeriod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PeriodEndYear",
                table: "CompanySalaryEntries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PeriodStartYear",
                table: "CompanySalaryEntries",
                type: "integer",
                nullable: true);

            // UTC on purpose: SubmittedAt is written in UTC, and the session time zone must not
            // decide which year a New Year's Eve submission belongs to.
            migrationBuilder.Sql("""
                UPDATE "CompanySalaryEntries"
                SET "PeriodStartYear" = EXTRACT(YEAR FROM ("SubmittedAt" AT TIME ZONE 'UTC'))::integer
                WHERE "EmploymentStatus" = 'CurrentEmployee' AND "PeriodStartYear" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PeriodEndYear",
                table: "CompanySalaryEntries");

            migrationBuilder.DropColumn(
                name: "PeriodStartYear",
                table: "CompanySalaryEntries");
        }
    }
}
