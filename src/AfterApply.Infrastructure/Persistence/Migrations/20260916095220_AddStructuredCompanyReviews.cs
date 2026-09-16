using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The structured review form (DECISIONS.md 2026-09-16). Additive by design: the legacy text
    /// and fixed-rating columns only lose NOT NULL, no row is updated or deleted, and the new
    /// <c>Format</c> column's database default labels every existing row — and any row an older
    /// instance inserts during the rollout — as Legacy.
    ///
    /// <c>Down</c> is a schema rollback only. It drops the two child tables (a structured review's
    /// category ratings and statement picks) and backfills the legacy columns with empty values
    /// so NOT NULL can be restored; once a structured review exists the right move is to roll
    /// forward, not back.
    /// </summary>
    public partial class AddStructuredCompanyReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "WorkEnvironmentRating",
                table: "CompanyReviews",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "CompanyReviews",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<int>(
                name: "SalaryAndBenefitsRating",
                table: "CompanyReviews",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Pros",
                table: "CompanyReviews",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<int>(
                name: "ManagementRating",
                table: "CompanyReviews",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Cons",
                table: "CompanyReviews",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<int>(
                name: "CareerAndDevelopmentRating",
                table: "CompanyReviews",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "Format",
                table: "CompanyReviews",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Legacy");

            migrationBuilder.CreateTable(
                name: "CompanyReviewCategoryRatings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyReviewCategoryRatings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyReviewCategoryRatings_CompanyReviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "CompanyReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompanyReviewStatementPicks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    StatementKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyReviewStatementPicks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyReviewStatementPicks_CompanyReviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "CompanyReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyReviewCategoryRatings_ReviewId_Category",
                table: "CompanyReviewCategoryRatings",
                columns: new[] { "ReviewId", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyReviewStatementPicks_ReviewId_StatementKey",
                table: "CompanyReviewStatementPicks",
                columns: new[] { "ReviewId", "StatementKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyReviewStatementPicks_StatementKey",
                table: "CompanyReviewStatementPicks",
                column: "StatementKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyReviewCategoryRatings");

            migrationBuilder.DropTable(
                name: "CompanyReviewStatementPicks");

            migrationBuilder.DropColumn(
                name: "Format",
                table: "CompanyReviews");

            migrationBuilder.AlterColumn<int>(
                name: "WorkEnvironmentRating",
                table: "CompanyReviews",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "CompanyReviews",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SalaryAndBenefitsRating",
                table: "CompanyReviews",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Pros",
                table: "CompanyReviews",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ManagementRating",
                table: "CompanyReviews",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Cons",
                table: "CompanyReviews",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CareerAndDevelopmentRating",
                table: "CompanyReviews",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
