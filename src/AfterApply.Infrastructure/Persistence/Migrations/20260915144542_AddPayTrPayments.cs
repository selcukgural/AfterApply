using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AfterApply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayTrPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiryReminderSentFor",
                table: "ProEntitlements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantOid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TotalAmountMinor = table.Column<long>(type: "bigint", nullable: true),
                    PaymentAmountMinor = table.Column<long>(type: "bigint", nullable: true),
                    PaymentType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    FailedReasonCode = table.Column<int>(type: "integer", nullable: true),
                    FailedReasonMsg = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TestMode = table.Column<bool>(type: "boolean", nullable: false),
                    HashValid = table.Column<bool>(type: "boolean", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RawForm = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentNotifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    MerchantOid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Plan = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BillingName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    BillingAddress = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    BillingPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Locale = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TermsAcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TermsVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IframeToken = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TokenExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedReasonCode = table.Column<int>(type: "integer", nullable: true),
                    FailedReasonMsg = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TotalAmountMinor = table.Column<long>(type: "bigint", nullable: true),
                    PaymentType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    TestMode = table.Column<bool>(type: "boolean", nullable: false),
                    AmountMismatch = table.Column<bool>(type: "boolean", nullable: false),
                    EntitlementActiveUntilBefore = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EntitlementActiveUntilAfter = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RefundRequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RefundReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RefundedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RefundedAmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    RefundReferenceNo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RefundedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RefundRejectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RefundRejectionNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentOrders_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "EmailTemplates",
                columns: new[] { "Id", "HtmlBody", "Key", "Locale", "Subject" },
                values: new object[,]
                {
                    { new Guid("5a1e0000-0000-4000-8000-000000000007"), "<div style=\"font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;\">\n  <h2>Ödemeniz alındı</h2>\n  <p><strong>{{PlanName}}</strong> için {{Amount}} tutarındaki ödemeniz onaylandı. Pro planınız <strong>{{ActiveUntil}}</strong> tarihine kadar aktif.</p>\n  <p>Otomatik yenileme yoktur; süre dolmadan birkaç gün önce size hatırlatırız.</p>\n  <p>\n    <a href=\"{{OrdersLink}}\" style=\"display:inline-block;padding:10px 20px;background:#2a5fd6;color:#fff;text-decoration:none;border-radius:6px;\">\n      Ödemelerimi gör\n    </a>\n  </p>\n  <p style=\"color:#555;font-size:13px;\">Kart bilgileriniz e-kariyerim'e hiç ulaşmaz; ödeme PayTR güvenli ödeme sayfasında alınmıştır.</p>\n</div>", "PaymentReceived", "tr", "Ödemeniz alındı — e-kariyerim Pro aktif" },
                    { new Guid("5a1e0000-0000-4000-8000-000000000008"), "<div style=\"font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;\">\n  <h2>Payment received</h2>\n  <p>Your payment of {{Amount}} for <strong>{{PlanName}}</strong> was confirmed. Your Pro plan is active until <strong>{{ActiveUntil}}</strong>.</p>\n  <p>There is no automatic renewal; we will remind you a few days before it ends.</p>\n  <p>\n    <a href=\"{{OrdersLink}}\" style=\"display:inline-block;padding:10px 20px;background:#2a5fd6;color:#fff;text-decoration:none;border-radius:6px;\">\n      See my payments\n    </a>\n  </p>\n  <p style=\"color:#555;font-size:13px;\">Your card details never reach e-kariyerim; the payment was taken on PayTR's secure payment page.</p>\n</div>", "PaymentReceived", "en", "Payment received — e-kariyerim Pro is active" },
                    { new Guid("5a1e0000-0000-4000-8000-000000000009"), "<div style=\"font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;\">\n  <h2>Pro süreniz bitmek üzere</h2>\n  <p>Pro planınız <strong>{{ActiveUntil}}</strong> tarihinde sona eriyor. Otomatik yenileme yoktur; haftalık ilan eşleştirmenin kesilmemesi için süreyi dilediğiniz zaman uzatabilirsiniz.</p>\n  <p>\n    <a href=\"{{RenewLink}}\" style=\"display:inline-block;padding:10px 20px;background:#2a5fd6;color:#fff;text-decoration:none;border-radius:6px;\">\n      Süreyi uzat\n    </a>\n  </p>\n  <p style=\"color:#555;font-size:13px;\">Uzatmazsanız hiçbir ücret alınmaz; verileriniz ve kriterleriniz hesabınızda kalır.</p>\n</div>", "ProExpiring", "tr", "e-kariyerim Pro süreniz {{ActiveUntil}} tarihinde bitiyor" },
                    { new Guid("5a1e0000-0000-4000-8000-00000000000a"), "<div style=\"font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;\">\n  <h2>Your Pro period is about to end</h2>\n  <p>Your Pro plan ends on <strong>{{ActiveUntil}}</strong>. There is no automatic renewal; extend it whenever you like so the weekly job matching keeps running.</p>\n  <p>\n    <a href=\"{{RenewLink}}\" style=\"display:inline-block;padding:10px 20px;background:#2a5fd6;color:#fff;text-decoration:none;border-radius:6px;\">\n      Extend my plan\n    </a>\n  </p>\n  <p style=\"color:#555;font-size:13px;\">If you do not extend, nothing is charged; your data and criteria stay in your account.</p>\n</div>", "ProExpiring", "en", "Your e-kariyerim Pro period ends on {{ActiveUntil}}" },
                    { new Guid("5a1e0000-0000-4000-8000-00000000000b"), "<div style=\"font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;\">\n  <h2>İadeniz yapıldı</h2>\n  <p>{{Amount}} tutarındaki iade, ödemeyi yaptığınız karta gönderildi. Bankanıza bağlı olarak hesabınıza yansıması 3–10 iş günü sürebilir.</p>\n  <p style=\"color:#555;font-size:13px;\">Sorunuz olursa bu e-postayı yanıtlayabilirsiniz.</p>\n</div>", "RefundCompleted", "tr", "İadeniz yapıldı — e-kariyerim" },
                    { new Guid("5a1e0000-0000-4000-8000-00000000000c"), "<div style=\"font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;\">\n  <h2>Your refund was sent</h2>\n  <p>A refund of {{Amount}} was sent to the card you paid with. Depending on your bank it can take 3–10 business days to appear.</p>\n  <p style=\"color:#555;font-size:13px;\">If you have a question, you can reply to this e-mail.</p>\n</div>", "RefundCompleted", "en", "Your refund was sent — e-kariyerim" },
                    { new Guid("5a1e0000-0000-4000-8000-00000000000d"), "<div style=\"font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;\">\n  <h2>İade talebiniz karşılanamadı</h2>\n  <p>İade talebinizi inceledik; bu ödeme için iade yapamıyoruz. Açıklama:</p>\n  <blockquote style=\"margin:0;padding:8px 12px;border-left:3px solid #ccc;color:#333;\">{{Note}}</blockquote>\n  <p style=\"color:#555;font-size:13px;\">Pro planınız süresi dolana kadar aktif kalır. Sorunuz olursa bu e-postayı yanıtlayabilirsiniz.</p>\n</div>", "RefundRejected", "tr", "İade talebiniz hakkında — e-kariyerim" },
                    { new Guid("5a1e0000-0000-4000-8000-00000000000e"), "<div style=\"font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;\">\n  <h2>We could not refund this payment</h2>\n  <p>We reviewed your refund request and cannot refund this payment. The reason given:</p>\n  <blockquote style=\"margin:0;padding:8px 12px;border-left:3px solid #ccc;color:#333;\">{{Note}}</blockquote>\n  <p style=\"color:#555;font-size:13px;\">Your Pro plan stays active until it ends. If you have a question, you can reply to this e-mail.</p>\n</div>", "RefundRejected", "en", "About your refund request — e-kariyerim" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentNotifications_MerchantOid_ReceivedAt",
                table: "PaymentNotifications",
                columns: new[] { "MerchantOid", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentNotifications_Outcome_ReceivedAt",
                table: "PaymentNotifications",
                columns: new[] { "Outcome", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentOrders_MerchantOid",
                table: "PaymentOrders",
                column: "MerchantOid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentOrders_PaidAt",
                table: "PaymentOrders",
                column: "PaidAt");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentOrders_Status_TokenExpiresAt",
                table: "PaymentOrders",
                columns: new[] { "Status", "TokenExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentOrders_UserId_CreatedAt",
                table: "PaymentOrders",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentNotifications");

            migrationBuilder.DropTable(
                name: "PaymentOrders");

            migrationBuilder.DeleteData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("5a1e0000-0000-4000-8000-000000000007"));

            migrationBuilder.DeleteData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("5a1e0000-0000-4000-8000-000000000008"));

            migrationBuilder.DeleteData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("5a1e0000-0000-4000-8000-000000000009"));

            migrationBuilder.DeleteData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("5a1e0000-0000-4000-8000-00000000000a"));

            migrationBuilder.DeleteData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("5a1e0000-0000-4000-8000-00000000000b"));

            migrationBuilder.DeleteData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("5a1e0000-0000-4000-8000-00000000000c"));

            migrationBuilder.DeleteData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("5a1e0000-0000-4000-8000-00000000000d"));

            migrationBuilder.DeleteData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("5a1e0000-0000-4000-8000-00000000000e"));

            migrationBuilder.DropColumn(
                name: "ExpiryReminderSentFor",
                table: "ProEntitlements");
        }
    }
}
