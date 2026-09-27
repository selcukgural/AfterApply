using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobLiveness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Jobs_Source_ExternalId",
                table: "Jobs");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosesAt",
                table: "Jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LivenessCheckedAt",
                table: "Jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LivenessSuspectedAt",
                table: "Jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Source_ExternalId",
                table: "Jobs",
                columns: new[] { "Source", "ExternalId" },
                unique: true,
                filter: "\"ExternalId\" IS NOT NULL AND \"ClosedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Jobs_Source_ExternalId",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "ClosesAt",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "LivenessCheckedAt",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "LivenessSuspectedAt",
                table: "Jobs");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Source_ExternalId",
                table: "Jobs",
                columns: new[] { "Source", "ExternalId" },
                unique: true,
                filter: "\"ExternalId\" IS NOT NULL");
        }
    }
}
