using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Locale = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HtmlBody = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailTemplates", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "EmailTemplates",
                columns: new[] { "Id", "HtmlBody", "Key", "Locale", "Subject" },
                values: new object[,]
                {
                    { new Guid("5a1e0000-0000-4000-8000-000000000001"), "<div style=\"font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;\">\n  <h2>e-kariyerim şifre sıfırlama</h2>\n  <p>Hesabınız için bir şifre sıfırlama talebi aldık. Aşağıdaki bağlantıya tıklayarak yeni bir şifre belirleyebilirsiniz. Bu bağlantı 30 dakika içinde geçerliliğini yitirecektir.</p>\n  <p>\n    <a href=\"{{ResetLink}}\" style=\"display:inline-block;padding:10px 20px;background:#2563eb;color:#fff;text-decoration:none;border-radius:6px;\">\n      Şifremi sıfırla\n    </a>\n  </p>\n  <p style=\"color:#555;font-size:13px;\">Bu talebi siz yapmadıysanız bu e-postayı yok sayabilirsiniz — hesabınızda herhangi bir değişiklik yapılmayacaktır.</p>\n</div>", "PasswordReset", "tr", "e-kariyerim şifre sıfırlama" },
                    { new Guid("5a1e0000-0000-4000-8000-000000000002"), "<div style=\"font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;\">\n  <h2>e-kariyerim password reset</h2>\n  <p>We received a request to reset your account's password. Click the button below to set a new password. This link will expire in 30 minutes.</p>\n  <p>\n    <a href=\"{{ResetLink}}\" style=\"display:inline-block;padding:10px 20px;background:#2563eb;color:#fff;text-decoration:none;border-radius:6px;\">\n      Reset my password\n    </a>\n  </p>\n  <p style=\"color:#555;font-size:13px;\">If you didn't request this, you can safely ignore this email — no changes will be made to your account.</p>\n</div>", "PasswordReset", "en", "e-kariyerim password reset" },
                    { new Guid("5a1e0000-0000-4000-8000-000000000003"), "<div style=\"font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;\">\n  <h2>e-kariyerim şifreniz değiştirildi</h2>\n  <p>e-kariyerim hesabınızın şifresi az önce değiştirildi ve diğer tüm cihazlardaki oturumlarınız güvenlik amacıyla sonlandırıldı.</p>\n  <p style=\"color:#555;font-size:13px;\">Bu işlemi siz yapmadıysanız lütfen hemen bizimle iletişime geçin.</p>\n</div>", "PasswordChanged", "tr", "e-kariyerim şifreniz değiştirildi" },
                    { new Guid("5a1e0000-0000-4000-8000-000000000004"), "<div style=\"font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;\">\n  <h2>Your e-kariyerim password was changed</h2>\n  <p>Your e-kariyerim account's password was just changed, and all sessions on your other devices have been signed out for security.</p>\n  <p style=\"color:#555;font-size:13px;\">If you didn't do this, please contact us immediately.</p>\n</div>", "PasswordChanged", "en", "Your e-kariyerim password was changed" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_Key_Locale",
                table: "EmailTemplates",
                columns: new[] { "Key", "Locale" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailTemplates");
        }
    }
}
