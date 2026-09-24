using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuthEmailDispatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthEmailDispatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuthEmailDispatches_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailVerificationChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CodeIssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedAttempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailVerificationChallenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailVerificationChallenges_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "EmailTemplates",
                columns: new[] { "Id", "HtmlBody", "Key", "Locale", "Subject" },
                values: new object[,]
                {
                    { new Guid("5a1e0000-0000-4000-8000-00000000000f"), "<div style=\"font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;\">\n  <h2>E-posta adresinizi doğrulayın</h2>\n  <p>e-kariyerim hesabınızı açmak için bu kodu, kaydı başlattığınız sayfaya girin:</p>\n  <p style=\"font-size:28px;font-weight:600;letter-spacing:6px;margin:16px 0;\">{{Code}}</p>\n  <p>Kod 15 dakika geçerlidir.</p>\n  <p style=\"color:#555;font-size:13px;\">Bu kaydı siz başlatmadıysanız bu e-postayı yok sayın ve kodu kimseyle paylaşmayın. Doğrulanmayan hesap 7 gün içinde silinir.</p>\n</div>", "EmailVerificationCode", "tr", "e-kariyerim doğrulama kodunuz: {{Code}}" },
                    { new Guid("5a1e0000-0000-4000-8000-000000000010"), "<div style=\"font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;\">\n  <h2>Verify your email address</h2>\n  <p>To open your e-kariyerim account, enter this code on the page where you started signing up:</p>\n  <p style=\"font-size:28px;font-weight:600;letter-spacing:6px;margin:16px 0;\">{{Code}}</p>\n  <p>The code works for 15 minutes.</p>\n  <p style=\"color:#555;font-size:13px;\">If you did not start this sign-up, ignore this email and don't share the code with anyone. An account that is never verified is deleted within 7 days.</p>\n</div>", "EmailVerificationCode", "en", "Your e-kariyerim verification code: {{Code}}" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuthEmailDispatches_Kind_SentAt",
                table: "AuthEmailDispatches",
                columns: new[] { "Kind", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthEmailDispatches_UserId_Kind_SentAt",
                table: "AuthEmailDispatches",
                columns: new[] { "UserId", "Kind", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationChallenges_TicketHash",
                table: "EmailVerificationChallenges",
                column: "TicketHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationChallenges_UserId",
                table: "EmailVerificationChallenges",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthEmailDispatches");

            migrationBuilder.DropTable(
                name: "EmailVerificationChallenges");

            migrationBuilder.DeleteData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("5a1e0000-0000-4000-8000-00000000000f"));

            migrationBuilder.DeleteData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("5a1e0000-0000-4000-8000-000000000010"));
        }
    }
}
