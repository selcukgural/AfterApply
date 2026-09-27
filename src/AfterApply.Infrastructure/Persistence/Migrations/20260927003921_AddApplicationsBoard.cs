using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationsBoard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BoardCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    TrackedJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    Position = table.Column<long>(type: "bigint", nullable: false),
                    Origin = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardCards", x => x.Id);
                    table.CheckConstraint("CK_BoardCards_ExactlyOneItem", "(\"ApplicationId\" IS NULL) <> (\"TrackedJobId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_BoardCards_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BoardCards_TrackedJobs_TrackedJobId",
                        column: x => x.TrackedJobId,
                        principalTable: "TrackedJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BoardCards_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BoardStates",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeededAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardStates", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_BoardStates_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BoardCards_ApplicationId",
                table: "BoardCards",
                column: "ApplicationId",
                unique: true,
                filter: "\"ApplicationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BoardCards_ClosedAt",
                table: "BoardCards",
                column: "ClosedAt",
                filter: "\"ClosedAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BoardCards_TrackedJobId",
                table: "BoardCards",
                column: "TrackedJobId",
                unique: true,
                filter: "\"TrackedJobId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BoardCards_UserId_Position_Id",
                table: "BoardCards",
                columns: new[] { "UserId", "Position", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BoardCards");

            migrationBuilder.DropTable(
                name: "BoardStates");
        }
    }
}
