using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Puts back the column defaults AddStatusChangeOrigin dropped. Dropping them was wrong: the
    /// migration job finishes before the new API revision takes traffic, and a Cloud Run rollback
    /// puts the old image back indefinitely — in both cases a release that predates
    /// StatusChangeOrigin is serving against this schema. That release's EF model has no Origin or
    /// Source property, so its INSERT omits both columns and relies on the default; without one,
    /// every status change and every new application fails with a not-null violation.
    ///
    /// 'Manual' is the honest fallback: it is what a write from a release that has no concept of
    /// provenance actually is. Current code always supplies both values explicitly (see
    /// StatusChangeContext), and the defaults are deliberately not declared on the EF model, so
    /// they only ever apply to writes from an older image.
    ///
    /// See DEPLOYMENT.md "Migrations must stay readable by the code that is still running".
    /// </summary>
    public partial class RestoreStatusHistoryColumnDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "ApplicationStatusHistories" ALTER COLUMN "Origin" SET DEFAULT 'Manual';
                ALTER TABLE "ApplicationStatusHistories" ALTER COLUMN "Source" SET DEFAULT 'Manual';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "ApplicationStatusHistories" ALTER COLUMN "Origin" DROP DEFAULT;
                ALTER TABLE "ApplicationStatusHistories" ALTER COLUMN "Source" DROP DEFAULT;
                """);
        }
    }
}
