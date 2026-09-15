using AfterApply.Domain.Payments;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class PaymentOrderConfiguration : IEntityTypeConfiguration<PaymentOrder>
{
    public void Configure(EntityTypeBuilder<PaymentOrder> builder)
    {
        builder.ToTable("PaymentOrders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.MerchantOid).IsRequired().HasMaxLength(PaymentOrder.MaxReferenceNoLength);
        builder.Property(o => o.Plan).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(o => o.Currency).IsRequired().HasMaxLength(3);
        builder.Property(o => o.Email).IsRequired().HasMaxLength(PaymentOrder.MaxEmailLength);
        builder.Property(o => o.BillingName).IsRequired().HasMaxLength(PaymentOrder.MaxBillingNameLength);
        builder.Property(o => o.BillingAddress).IsRequired().HasMaxLength(PaymentOrder.MaxBillingAddressLength);
        builder.Property(o => o.BillingPhone).IsRequired().HasMaxLength(PaymentOrder.MaxBillingPhoneLength);
        builder.Property(o => o.Locale).IsRequired().HasMaxLength(10);
        builder.Property(o => o.TermsVersion).IsRequired().HasMaxLength(PaymentOrder.MaxTermsVersionLength);
        builder.Property(o => o.IframeToken).HasMaxLength(PaymentOrder.MaxTokenLength);
        builder.Property(o => o.FailedReasonMsg).HasMaxLength(PaymentOrder.MaxFailedReasonLength);
        builder.Property(o => o.PaymentType).HasMaxLength(PaymentOrder.MaxPaymentTypeLength);
        builder.Property(o => o.RefundReason).HasMaxLength(PaymentOrder.MaxRefundReasonLength);
        builder.Property(o => o.RefundReferenceNo).HasMaxLength(PaymentOrder.MaxReferenceNoLength);
        builder.Property(o => o.RefundRejectionNote).HasMaxLength(PaymentOrder.MaxRefundNoteLength);

        // The notification only carries merchant_oid; this is the lookup it does.
        builder.HasIndex(o => o.MerchantOid).IsUnique();
        // A user's order history, newest first.
        builder.HasIndex(o => new { o.UserId, o.CreatedAt });
        // The expiry job's scan: pending orders whose window closed.
        builder.HasIndex(o => new { o.Status, o.TokenExpiresAt });
        // Monthly totals group by the month of payment.
        builder.HasIndex(o => o.PaidAt);

        // Two notifications for the same order can land on two instances at once; whichever
        // saves second sees the row changed and re-reads instead of extending the entitlement twice.
        builder.Property<uint>("xmin").IsRowVersion();

        // A financial record outlives the account (compare AiUsageEntryConfiguration): the month's
        // revenue must not shrink because a user left, and invoicing records are kept for the
        // statutory period. The billing fields stay for that reason; the privacy text says so.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
