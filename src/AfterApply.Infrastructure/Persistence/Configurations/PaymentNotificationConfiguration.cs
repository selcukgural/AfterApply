using AfterApply.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class PaymentNotificationConfiguration : IEntityTypeConfiguration<PaymentNotification>
{
    public void Configure(EntityTypeBuilder<PaymentNotification> builder)
    {
        builder.ToTable("PaymentNotifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.MerchantOid).IsRequired().HasMaxLength(PaymentOrder.MaxReferenceNoLength);
        builder.Property(n => n.Status).IsRequired().HasMaxLength(PaymentNotification.MaxStatusLength);
        builder.Property(n => n.PaymentType).HasMaxLength(PaymentOrder.MaxPaymentTypeLength);
        builder.Property(n => n.FailedReasonMsg).HasMaxLength(PaymentOrder.MaxFailedReasonLength);
        builder.Property(n => n.Outcome).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(n => n.RawForm).IsRequired().HasMaxLength(PaymentNotification.MaxRawFormLength);

        // The order detail's timeline, and the admin's "what did we answer PayTR" lookup.
        builder.HasIndex(n => new { n.MerchantOid, n.ReceivedAt });
        // The alerts view: bad hashes and unknown orders in the last N days.
        builder.HasIndex(n => new { n.Outcome, n.ReceivedAt });

        // No FK to the order on purpose: a notification for an order we do not know is exactly
        // the case this table exists to record.
    }
}
