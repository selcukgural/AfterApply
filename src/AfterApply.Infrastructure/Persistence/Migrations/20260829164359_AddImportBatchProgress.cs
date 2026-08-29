using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddImportBatchProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "ImportBatches",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "ImportBatches",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessedRows",
                table: "ImportBatches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Every pre-existing row was written by the old fully-synchronous import path, which
            // only ever persisted a batch after it had already finished — so backfill them as
            // Completed rather than defaulting to an empty/invalid status.
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ImportBatches",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Completed");

            migrationBuilder.AddColumn<int>(
                name: "TotalRows",
                table: "ImportBatches",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "ImportBatches");

            migrationBuilder.DropColumn(
                name: "ProcessedRows",
                table: "ImportBatches");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ImportBatches");

            migrationBuilder.DropColumn(
                name: "TotalRows",
                table: "ImportBatches");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "ImportBatches",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }
    }
}
