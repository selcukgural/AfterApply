using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyLogoRecheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeferCount",
                table: "CompanyLogos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextCheckAt",
                table: "CompanyLogos",
                type: "timestamp with time zone",
                nullable: true);

            // A "not found" recorded before this column existed was due again a month after it
            // was checked (the old RetryAfter); keep that date rather than asking everyone tonight.
            migrationBuilder.Sql("""
                UPDATE "CompanyLogos"
                SET "NextCheckAt" = "CheckedAt" + interval '30 days'
                WHERE "Content" IS NULL AND NOT "Blocked";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeferCount",
                table: "CompanyLogos");

            migrationBuilder.DropColumn(
                name: "NextCheckAt",
                table: "CompanyLogos");
        }
    }
}
