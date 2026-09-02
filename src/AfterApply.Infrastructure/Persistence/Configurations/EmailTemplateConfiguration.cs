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
            });
    }
}
