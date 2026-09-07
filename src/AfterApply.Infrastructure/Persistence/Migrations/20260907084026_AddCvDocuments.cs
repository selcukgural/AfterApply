using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCvDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CvDocumentId",
                table: "Applications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CvDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StorageObjectName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Format = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CvDocuments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_CvDocumentId",
                table: "Applications",
                column: "CvDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_CvDocuments_UserId",
                table: "CvDocuments",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_CvDocuments_CvDocumentId",
                table: "Applications",
                column: "CvDocumentId",
                principalTable: "CvDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Applications_CvDocuments_CvDocumentId",
                table: "Applications");

            migrationBuilder.DropTable(
                name: "CvDocuments");

            migrationBuilder.DropIndex(
                name: "IX_Applications_CvDocumentId",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "CvDocumentId",
                table: "Applications");
        }
    }
}
