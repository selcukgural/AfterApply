using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHrContactFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HrEmail",
                table: "TrackedJobs",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HrLinkedInUrl",
                table: "TrackedJobs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HrName",
                table: "TrackedJobs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HrEmail",
                table: "Applications",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HrLinkedInUrl",
                table: "Applications",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HrName",
                table: "Applications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HrEmail",
                table: "TrackedJobs");

            migrationBuilder.DropColumn(
                name: "HrLinkedInUrl",
                table: "TrackedJobs");

            migrationBuilder.DropColumn(
                name: "HrName",
                table: "TrackedJobs");

            migrationBuilder.DropColumn(
                name: "HrEmail",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "HrLinkedInUrl",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "HrName",
                table: "Applications");
        }
    }
}
