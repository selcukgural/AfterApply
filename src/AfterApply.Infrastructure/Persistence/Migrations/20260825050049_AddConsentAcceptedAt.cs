using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConsentAcceptedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No real beta users exist in any environment yet, so any pre-existing row
            // (local dev data) is backfilled with the migration's own run time rather
            // than a sentinel like DateTimeOffset.MinValue — see Sprint 7 plan.
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConsentAcceptedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsentAcceptedAt",
                table: "Users");
        }
    }
}
