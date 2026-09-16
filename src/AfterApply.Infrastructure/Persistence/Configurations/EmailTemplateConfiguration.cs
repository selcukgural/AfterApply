using AfterApply.Domain.Mailing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
{
    // Fixed on purpose (not Guid.CreateVersion7()) — HasData seeds are matched by Id across
    // migrations, a random value here would make every future `dotnet ef migrations add` see
    // these as newly-added rows instead of no-ops.
    private static readonly Guid PasswordResetTrId = new("5a1e0000-0000-4000-8000-000000000001");
    private static readonly Guid PasswordResetEnId = new("5a1e0000-0000-4000-8000-000000000002");
    private static readonly Guid PasswordChangedTrId = new("5a1e0000-0000-4000-8000-000000000003");
    private static readonly Guid PasswordChangedEnId = new("5a1e0000-0000-4000-8000-000000000004");
    private static readonly Guid WeeklyJobsReadyTrId = new("5a1e0000-0000-4000-8000-000000000005");
    private static readonly Guid WeeklyJobsReadyEnId = new("5a1e0000-0000-4000-8000-000000000006");
    private static readonly Guid PaymentReceivedTrId = new("5a1e0000-0000-4000-8000-000000000007");
    private static readonly Guid PaymentReceivedEnId = new("5a1e0000-0000-4000-8000-000000000008");
    private static readonly Guid ProExpiringTrId = new("5a1e0000-0000-4000-8000-000000000009");
    private static readonly Guid ProExpiringEnId = new("5a1e0000-0000-4000-8000-00000000000a");
    private static readonly Guid RefundCompletedTrId = new("5a1e0000-0000-4000-8000-00000000000b");
    private static readonly Guid RefundCompletedEnId = new("5a1e0000-0000-4000-8000-00000000000c");
    private static readonly Guid RefundRejectedTrId = new("5a1e0000-0000-4000-8000-00000000000d");
    private static readonly Guid RefundRejectedEnId = new("5a1e0000-0000-4000-8000-00000000000e");

    public void Configure(EntityTypeBuilder<EmailTemplate> builder)
    {
        builder.ToTable("EmailTemplates");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Key).HasConversion<string>().HasMaxLength(50);
        builder.Property(t => t.Locale).IsRequired().HasMaxLength(10);
        builder.Property(t => t.Subject).IsRequired().HasMaxLength(200);
        builder.Property(t => t.HtmlBody).IsRequired();

        builder.HasIndex(t => new { t.Key, t.Locale }).IsUnique();

        // Seeded so the feature works out of the box; from here on, editing a row in the
        // EmailTemplates table takes effect on the next send — no redeploy needed. Placeholder
        // "{{ResetLink}}" (PasswordReset only) is substituted by ResendEmailSender before sending.
        builder.HasData(
            new
            {
                Id = PasswordResetTrId,
                Key = EmailTemplateKey.PasswordReset,
                Locale = "tr",
                Subject = "e-kariyerim şifre sıfırlama",
                HtmlBody = """
                    <div style="font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;">
                      <h2>e-kariyerim şifre sıfırlama</h2>
                      <p>Hesabınız için bir şifre sıfırlama talebi aldık. Aşağıdaki bağlantıya tıklayarak yeni bir şifre belirleyebilirsiniz. Bu bağlantı 30 dakika içinde geçerliliğini yitirecektir.</p>
                      <p>
                        <a href="{{ResetLink}}" style="display:inline-block;padding:10px 20px;background:#2563eb;color:#fff;text-decoration:none;border-radius:6px;">
                          Şifremi sıfırla
                        </a>
                      </p>
                      <p style="color:#555;font-size:13px;">Bu talebi siz yapmadıysanız bu e-postayı yok sayabilirsiniz — hesabınızda herhangi bir değişiklik yapılmayacaktır.</p>
                    </div>
                    """
            },
            new
            {
                Id = PasswordResetEnId,
                Key = EmailTemplateKey.PasswordReset,
                Locale = "en",
                Subject = "e-kariyerim password reset",
                HtmlBody = """
                    <div style="font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;">
                      <h2>e-kariyerim password reset</h2>
                      <p>We received a request to reset your account's password. Click the button below to set a new password. This link will expire in 30 minutes.</p>
                      <p>
                        <a href="{{ResetLink}}" style="display:inline-block;padding:10px 20px;background:#2563eb;color:#fff;text-decoration:none;border-radius:6px;">
                          Reset my password
                        </a>
                      </p>
                      <p style="color:#555;font-size:13px;">If you didn't request this, you can safely ignore this email — no changes will be made to your account.</p>
                    </div>
                    """
            },
            new
            {
                Id = PasswordChangedTrId,
                Key = EmailTemplateKey.PasswordChanged,
                Locale = "tr",
                Subject = "e-kariyerim şifreniz değiştirildi",
                HtmlBody = """
                    <div style="font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;">
                      <h2>e-kariyerim şifreniz değiştirildi</h2>
                      <p>e-kariyerim hesabınızın şifresi az önce değiştirildi ve diğer tüm cihazlardaki oturumlarınız güvenlik amacıyla sonlandırıldı.</p>
                      <p style="color:#555;font-size:13px;">Bu işlemi siz yapmadıysanız lütfen hemen bizimle iletişime geçin.</p>
                    </div>
                    """
            },
            new
            {
                Id = PasswordChangedEnId,
                Key = EmailTemplateKey.PasswordChanged,
                Locale = "en",
                Subject = "Your e-kariyerim password was changed",
                HtmlBody = """
                    <div style="font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;">
                      <h2>Your e-kariyerim password was changed</h2>
                      <p>Your e-kariyerim account's password was just changed, and all sessions on your other devices have been signed out for security.</p>
                      <p style="color:#555;font-size:13px;">If you didn't do this, please contact us immediately.</p>
                    </div>
                    """
            },
            // The weekly job matching's Monday digest. {{BestTitle}}/{{BestCompany}} are scraped
            // text and are HTML-encoded by ResendEmailSender before substitution.
            new
            {
                Id = WeeklyJobsReadyTrId,
                Key = EmailTemplateKey.WeeklyJobsReady,
                Locale = "tr",
                Subject = "Bu hafta size uyan {{Count}} ilan hazır",
                HtmlBody = """
                    <div style="font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;">
                      <h2>Bu hafta {{Count}} ilan hazır</h2>
                      <p>Kaydettiğiniz kriterlerle bulunan yeni ilanlar CV'nizle karşılaştırıldı ve uyum puanına göre sıralandı.</p>
                      <p>En yüksek uyum: <strong>{{BestTitle}}</strong> — {{BestCompany}} (%{{BestScore}}).</p>
                      <p>
                        <a href="{{Link}}" style="display:inline-block;padding:10px 20px;background:#2a5fd6;color:#fff;text-decoration:none;border-radius:6px;">
                          İlanları gör
                        </a>
                      </p>
                      <p style="color:#555;font-size:13px;">Bu e-postayı haftalık ilan eşleştirmeyi açtığınız için alıyorsunuz. Kriterler sayfasından kapatabilirsiniz.</p>
                    </div>
                    """
            },
            new
            {
                Id = WeeklyJobsReadyEnId,
                Key = EmailTemplateKey.WeeklyJobsReady,
                Locale = "en",
                Subject = "{{Count}} postings that fit you are ready this week",
                HtmlBody = """
                    <div style="font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;">
                      <h2>{{Count}} postings are ready this week</h2>
                      <p>The new postings found with your saved criteria were compared with your CV and ordered by fit.</p>
                      <p>Best fit: <strong>{{BestTitle}}</strong> — {{BestCompany}} ({{BestScore}}%).</p>
                      <p>
                        <a href="{{Link}}" style="display:inline-block;padding:10px 20px;background:#2a5fd6;color:#fff;text-decoration:none;border-radius:6px;">
                          See the postings
                        </a>
                      </p>
                      <p style="color:#555;font-size:13px;">You receive this because you turned on the weekly job matching. You can switch it off on the criteria page.</p>
                    </div>
                    """
            },
            // Payments. {{Amount}} and {{ActiveUntil}} are formatted by the sender in the user's
            // locale; {{Note}} is text an admin typed and is HTML-encoded before substitution.
            new
            {
                Id = PaymentReceivedTrId,
                Key = EmailTemplateKey.PaymentReceived,
                Locale = "tr",
                Subject = "Ödemeniz alındı — e-kariyerim Pro aktif",
                HtmlBody = """
                    <div style="font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;">
                      <h2>Ödemeniz alındı</h2>
                      <p><strong>{{PlanName}}</strong> için {{Amount}} tutarındaki ödemeniz onaylandı. Pro planınız <strong>{{ActiveUntil}}</strong> tarihine kadar aktif.</p>
                      <p>Otomatik yenileme yoktur; süre dolmadan birkaç gün önce size hatırlatırız.</p>
                      <p>
                        <a href="{{OrdersLink}}" style="display:inline-block;padding:10px 20px;background:#2a5fd6;color:#fff;text-decoration:none;border-radius:6px;">
                          Ödemelerimi gör
                        </a>
                      </p>
                      <p style="color:#555;font-size:13px;">Kart bilgileriniz e-kariyerim'e hiç ulaşmaz; ödeme PayTR güvenli ödeme sayfasında alınmıştır.</p>
                    </div>
                    """
            },
            new
            {
                Id = PaymentReceivedEnId,
                Key = EmailTemplateKey.PaymentReceived,
                Locale = "en",
                Subject = "Payment received — e-kariyerim Pro is active",
                HtmlBody = """
                    <div style="font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;">
                      <h2>Payment received</h2>
                      <p>Your payment of {{Amount}} for <strong>{{PlanName}}</strong> was confirmed. Your Pro plan is active until <strong>{{ActiveUntil}}</strong>.</p>
                      <p>There is no automatic renewal; we will remind you a few days before it ends.</p>
                      <p>
                        <a href="{{OrdersLink}}" style="display:inline-block;padding:10px 20px;background:#2a5fd6;color:#fff;text-decoration:none;border-radius:6px;">
                          See my payments
                        </a>
                      </p>
                      <p style="color:#555;font-size:13px;">Your card details never reach e-kariyerim; the payment was taken on PayTR's secure payment page.</p>
                    </div>
                    """
            },
            new
            {
                Id = ProExpiringTrId,
                Key = EmailTemplateKey.ProExpiring,
                Locale = "tr",
                Subject = "e-kariyerim Pro süreniz {{ActiveUntil}} tarihinde bitiyor",
                HtmlBody = """
                    <div style="font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;">
                      <h2>Pro süreniz bitmek üzere</h2>
                      <p>Pro planınız <strong>{{ActiveUntil}}</strong> tarihinde sona eriyor. Otomatik yenileme yoktur; haftalık ilan eşleştirmenin kesilmemesi için süreyi dilediğiniz zaman uzatabilirsiniz.</p>
                      <p>
                        <a href="{{RenewLink}}" style="display:inline-block;padding:10px 20px;background:#2a5fd6;color:#fff;text-decoration:none;border-radius:6px;">
                          Süreyi uzat
                        </a>
                      </p>
                      <p style="color:#555;font-size:13px;">Uzatmazsanız hiçbir ücret alınmaz; verileriniz ve kriterleriniz hesabınızda kalır.</p>
                    </div>
                    """
            },
            new
            {
                Id = ProExpiringEnId,
                Key = EmailTemplateKey.ProExpiring,
                Locale = "en",
                Subject = "Your e-kariyerim Pro period ends on {{ActiveUntil}}",
                HtmlBody = """
                    <div style="font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;">
                      <h2>Your Pro period is about to end</h2>
                      <p>Your Pro plan ends on <strong>{{ActiveUntil}}</strong>. There is no automatic renewal; extend it whenever you like so the weekly job matching keeps running.</p>
                      <p>
                        <a href="{{RenewLink}}" style="display:inline-block;padding:10px 20px;background:#2a5fd6;color:#fff;text-decoration:none;border-radius:6px;">
                          Extend my plan
                        </a>
                      </p>
                      <p style="color:#555;font-size:13px;">If you do not extend, nothing is charged; your data and criteria stay in your account.</p>
                    </div>
                    """
            },
            new
            {
                Id = RefundCompletedTrId,
                Key = EmailTemplateKey.RefundCompleted,
                Locale = "tr",
                Subject = "İadeniz yapıldı — e-kariyerim",
                HtmlBody = """
                    <div style="font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;">
                      <h2>İadeniz yapıldı</h2>
                      <p>{{Amount}} tutarındaki iade, ödemeyi yaptığınız karta gönderildi. Bankanıza bağlı olarak hesabınıza yansıması 3–10 iş günü sürebilir.</p>
                      <p style="color:#555;font-size:13px;">Sorunuz olursa bu e-postayı yanıtlayabilirsiniz.</p>
                    </div>
                    """
            },
            new
            {
                Id = RefundCompletedEnId,
                Key = EmailTemplateKey.RefundCompleted,
                Locale = "en",
                Subject = "Your refund was sent — e-kariyerim",
                HtmlBody = """
                    <div style="font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;">
                      <h2>Your refund was sent</h2>
                      <p>A refund of {{Amount}} was sent to the card you paid with. Depending on your bank it can take 3–10 business days to appear.</p>
                      <p style="color:#555;font-size:13px;">If you have a question, you can reply to this e-mail.</p>
                    </div>
                    """
            },
            new
            {
                Id = RefundRejectedTrId,
                Key = EmailTemplateKey.RefundRejected,
                Locale = "tr",
                Subject = "İade talebiniz hakkında — e-kariyerim",
                HtmlBody = """
                    <div style="font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;">
                      <h2>İade talebiniz karşılanamadı</h2>
                      <p>İade talebinizi inceledik; bu ödeme için iade yapamıyoruz. Açıklama:</p>
                      <blockquote style="margin:0;padding:8px 12px;border-left:3px solid #ccc;color:#333;">{{Note}}</blockquote>
                      <p style="color:#555;font-size:13px;">Pro planınız süresi dolana kadar aktif kalır. Sorunuz olursa bu e-postayı yanıtlayabilirsiniz.</p>
                    </div>
                    """
            },
            new
            {
                Id = RefundRejectedEnId,
                Key = EmailTemplateKey.RefundRejected,
                Locale = "en",
                Subject = "About your refund request — e-kariyerim",
                HtmlBody = """
                    <div style="font-family:sans-serif;max-width:480px;margin:0 auto;color:#111;">
                      <h2>We could not refund this payment</h2>
                      <p>We reviewed your refund request and cannot refund this payment. The reason given:</p>
                      <blockquote style="margin:0;padding:8px 12px;border-left:3px solid #ccc;color:#333;">{{Note}}</blockquote>
                      <p style="color:#555;font-size:13px;">Your Pro plan stays active until it ends. If you have a question, you can reply to this e-mail.</p>
                    </div>
                    """
            });
    }
}
