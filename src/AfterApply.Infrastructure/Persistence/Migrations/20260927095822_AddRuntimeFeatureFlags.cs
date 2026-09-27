using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRuntimeFeatureFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeatureFlagChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: true),
                    WasOn = table.Column<bool>(type: "boolean", nullable: false),
                    IsOn = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureFlagChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeatureFlagChanges_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "FeatureFlagOverrides",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureFlagOverrides", x => x.Key);
                    table.ForeignKey(
                        name: "FK_FeatureFlagOverrides_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "FeatureFlagChangeOrigins",
                columns: table => new
                {
                    ChangeId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureFlagChangeOrigins", x => x.ChangeId);
                    table.ForeignKey(
                        name: "FK_FeatureFlagChangeOrigins_FeatureFlagChanges_ChangeId",
                        column: x => x.ChangeId,
                        principalTable: "FeatureFlagChanges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FeatureFlagChangeOrigins_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlagChangeOrigins_UserId",
                table: "FeatureFlagChangeOrigins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlagChanges_ChangedAt",
                table: "FeatureFlagChanges",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlagChanges_ChangedByUserId",
                table: "FeatureFlagChanges",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlagChanges_Key_ChangedAt",
                table: "FeatureFlagChanges",
                columns: new[] { "Key", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlagOverrides_UpdatedByUserId",
                table: "FeatureFlagOverrides",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeatureFlagChangeOrigins");

            migrationBuilder.DropTable(
                name: "FeatureFlagOverrides");

            migrationBuilder.DropTable(
                name: "FeatureFlagChanges");
        }
    }
}
