using AfterApply.Application.Localization;
using AfterApply.Application.Payments.Contracts;
using AfterApply.Domain.Payments;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace AfterApply.Application.Payments.Validators;

public sealed class StartCheckoutRequestValidator : AbstractValidator<StartCheckoutRequest>
{
    public StartCheckoutRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.Plan).Must(plan => Enum.TryParse<ProPlan>(plan, ignoreCase: true, out _))
            .WithMessage(_ => localizer["PAYMENT_PLAN_UNKNOWN"]);
        RuleFor(x => x.BillingName).Must(v => BePlainText(v, PaymentOrder.MaxBillingNameLength))
            .WithMessage(_ => localizer["VALIDATION_BILLING_NAME"]);
        RuleFor(x => x.BillingAddress).Must(v => BePlainText(v, PaymentOrder.MaxBillingAddressLength))
            .WithMessage(_ => localizer["VALIDATION_BILLING_ADDRESS"]);
        RuleFor(x => x.BillingPhone).Must(BePhone)
            .WithMessage(_ => localizer["VALIDATION_BILLING_PHONE"]);
        RuleFor(x => x.AcceptTerms).Must(x => x).WithMessage(_ => localizer["PAYMENT_TERMS_REQUIRED"]);
    }

    // One rule per field, so the single localized message covers empty, too long and control
    // characters alike (FluentValidation's WithMessage only names the rule right before it).
    private static bool BePlainText(string? value, int max) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= max && !value.Any(char.IsControl);

    // Digits, spaces and the usual punctuation of a phone number; PayTR only wants it non-empty
    // and ≤ 20 characters, the shape check is so an invoice does not end up with "asdf".
    private static bool BePhone(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= PaymentOrder.MaxBillingPhoneLength
        && value.Any(char.IsDigit) && value.All(c => char.IsDigit(c) || c is ' ' or '+' or '-' or '(' or ')');
}

public sealed class RequestRefundRequestValidator : AbstractValidator<RequestRefundRequest>
{
    public RequestRefundRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.Reason).Must(r => !string.IsNullOrWhiteSpace(r) && r.Length <= PaymentOrder.MaxRefundReasonLength)
            .WithMessage(_ => localizer["VALIDATION_REFUND_REASON"]);
    }
}

public sealed class AdminRefundRequestValidator : AbstractValidator<AdminRefundRequest>
{
    public AdminRefundRequestValidator()
    {
        RuleFor(x => x.AmountMinor).GreaterThan(0).When(x => x.AmountMinor is not null);
    }
}

public sealed class RejectRefundRequestValidator : AbstractValidator<RejectRefundRequest>
{
    public RejectRefundRequestValidator()
    {
        RuleFor(x => x.Note).NotEmpty().MaximumLength(PaymentOrder.MaxRefundNoteLength);
    }
}

public sealed class MarkRefundedRequestValidator : AbstractValidator<MarkRefundedRequest>
{
    public MarkRefundedRequestValidator()
    {
        RuleFor(x => x.AmountMinor).GreaterThan(0);
        RuleFor(x => x.ReferenceNo).NotEmpty().MaximumLength(PaymentOrder.MaxReferenceNoLength);
    }
}
