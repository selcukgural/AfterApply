namespace AfterApply.Application.Payments.Contracts;

/// <summary>What the checkout form sends. The billing fields are PayTR's required
/// user_name/user_address/user_phone and what an invoice needs; <paramref name="AcceptTerms"/>
/// is the distance-sales checkbox and must be true.</summary>
public sealed record StartCheckoutRequest(string Plan, string BillingName, string BillingAddress, string BillingPhone, bool AcceptTerms);

public sealed record RequestRefundRequest(string Reason);

/// <summary>Admin refund. <paramref name="AmountMinor"/> null means everything still refundable.</summary>
public sealed record AdminRefundRequest(long? AmountMinor);

public sealed record RejectRefundRequest(string Note);

/// <summary>The admin verified in the PayTR merchant panel that a refund went through although
/// our own record of it failed to commit; this records it without calling PayTR again.</summary>
public sealed record MarkRefundedRequest(long AmountMinor, string ReferenceNo);
