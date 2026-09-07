using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductMetricsDailySnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductMetricsDailySnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalUsers = table.Column<int>(type: "integer", nullable: false),
                    ActivatedUsers = table.Column<int>(type: "integer", nullable: false),
                    ActivationRate = table.Column<double>(type: "double precision", nullable: false),
                    WeeklyActiveUsers = table.Column<int>(type: "integer", nullable: false),
                    ApplicationsTrackedLast30Days = table.Column<int>(type: "integer", nullable: false),
                    StatusUpdatesLast30Days = table.Column<int>(type: "integer", nullable: false),
                    D7RetentionRate = table.Column<double>(type: "double precision", nullable: true),
                    D30RetentionRate = table.Column<double>(type: "double precision", nullable: true),
                    D90RetentionRate = table.Column<double>(type: "double precision", nullable: true),
                    TotalApplications = table.Column<int>(type: "integer", nullable: false),
                    UniqueCompanies = table.Column<int>(type: "integer", nullable: false),
                    UniqueJobs = table.Column<int>(type: "integer", nullable: false),
                    ApplicationsWithOutcome = table.Column<int>(type: "integer", nullable: false),
                    ApplicationsWithResponseTime = table.Column<int>(type: "integer", nullable: false),
                    ComputedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductMetricsDailySnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductMetricsDailySnapshots_SnapshotDate",
                table: "ProductMetricsDailySnapshots",
                column: "SnapshotDate",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductMetricsDailySnapshots");
        }
    }
}
