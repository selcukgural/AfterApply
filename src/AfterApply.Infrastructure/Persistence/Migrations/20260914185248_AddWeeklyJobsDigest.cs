using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyJobsDigest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DigestSentAt",
                table: "UserJobSourceRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmailDigestEnabled",
                table: "UserJobSourceProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.InsertData(
                table: "EmailTemplates",
                columns: new[] { "Id", "HtmlBody", "Key", "Locale", "Subject" },
                values: new object[,]
                {
                    { new Guid("5a1e0000-0000-4000-8000-000000000005"), "<div style=\"font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;\">\n  <h2>Bu hafta {{Count}} ilan hazır</h2>\n  <p>Kaydettiğiniz kriterlerle bulunan yeni ilanlar CV'nizle karşılaştırıldı ve uyum puanına göre sıralandı.</p>\n  <p>En yüksek uyum: <strong>{{BestTitle}}</strong> — {{BestCompany}} (%{{BestScore}}).</p>\n  <p>\n    <a href=\"{{Link}}\" style=\"display:inline-block;padding:10px 20px;background:#2a5fd6;color:#fff;text-decoration:none;border-radius:6px;\">\n      İlanları gör\n    </a>\n  </p>\n  <p style=\"color:#555;font-size:13px;\">Bu e-postayı haftalık ilan eşleştirmeyi açtığınız için alıyorsunuz. Kriterler sayfasından kapatabilirsiniz.</p>\n</div>", "WeeklyJobsReady", "tr", "Bu hafta size uyan {{Count}} ilan hazır" },
                    { new Guid("5a1e0000-0000-4000-8000-000000000006"), "<div style=\"font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;\">\n  <h2>{{Count}} postings are ready this week</h2>\n  <p>The new postings found with your saved criteria were compared with your CV and ordered by fit.</p>\n  <p>Best fit: <strong>{{BestTitle}}</strong> — {{BestCompany}} ({{BestScore}}%).</p>\n  <p>\n    <a href=\"{{Link}}\" style=\"display:inline-block;padding:10px 20px;background:#2a5fd6;color:#fff;text-decoration:none;border-radius:6px;\">\n      See the postings\n    </a>\n  </p>\n  <p style=\"color:#555;font-size:13px;\">You receive this because you turned on the weekly job matching. You can switch it off on the criteria page.</p>\n</div>", "WeeklyJobsReady", "en", "{{Count}} postings that fit you are ready this week" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("5a1e0000-0000-4000-8000-000000000005"));

            migrationBuilder.DeleteData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("5a1e0000-0000-4000-8000-000000000006"));

            migrationBuilder.DropColumn(
                name: "DigestSentAt",
                table: "UserJobSourceRuns");

            migrationBuilder.DropColumn(
                name: "EmailDigestEnabled",
                table: "UserJobSourceProfiles");
        }
    }
}
