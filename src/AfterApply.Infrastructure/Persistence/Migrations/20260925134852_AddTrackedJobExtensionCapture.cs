using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackedJobExtensionCapture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CapturedJobDescriptionHtml",
                table: "TrackedJobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "JobId",
                table: "TrackedJobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrackedJobs_JobId",
                table: "TrackedJobs",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedJobs_UserId_JobUrl",
                table: "TrackedJobs",
                columns: new[] { "UserId", "JobUrl" });

            migrationBuilder.AddForeignKey(
                name: "FK_TrackedJobs_Jobs_JobId",
                table: "TrackedJobs",
                column: "JobId",
                principalTable: "Jobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TrackedJobs_Jobs_JobId",
                table: "TrackedJobs");

            migrationBuilder.DropIndex(
                name: "IX_TrackedJobs_JobId",
                table: "TrackedJobs");

            migrationBuilder.DropIndex(
                name: "IX_TrackedJobs_UserId_JobUrl",
                table: "TrackedJobs");

            migrationBuilder.DropColumn(
                name: "CapturedJobDescriptionHtml",
                table: "TrackedJobs");

            migrationBuilder.DropColumn(
                name: "JobId",
                table: "TrackedJobs");
        }
    }
}
