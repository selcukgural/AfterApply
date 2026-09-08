using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBenchmarkSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BenchmarkSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationCount = table.Column<int>(type: "integer", nullable: false),
                    ReplyCount = table.Column<int>(type: "integer", nullable: false),
                    Sector = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Period = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Seniority = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Location = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Locale = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenchmarkSubmissions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BenchmarkSubmissions_Sector",
                table: "BenchmarkSubmissions",
                column: "Sector");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BenchmarkSubmissions");
        }
    }
}
