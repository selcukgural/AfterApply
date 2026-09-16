import type { PaymentOrderResponse } from "@/types/api";

/**
 * PayTR's step-2 `failed_reason_code` table (dev.paytr.com, "2. Adım İçin Hata Kodları"), mapped
 * to our own message keys so the user reads the reason in their language. Code 0 is "the bank
 * said no, here is its text" — the text is Turkish and comes from the bank, so it is shown as-is
 * under a generic line rather than translated. Codes we do not know fall back to `unknown`.
 * -1 is ours: the token request itself was refused, no card was ever shown.
 */
const KNOWN_CODES = new Set([1, 2, 3, 6, 8, 9, 10, 11, 99]);

export type FailureExplanation = {
  /** A key under `payments.failure.*`. */
  messageKey: string;
  /** The bank's own wording, only for code 0. Plain text — render as text, never as HTML. */
  bankMessage: string | null;
  /** Whether "try again" is a sensible next step. */
  retryable: boolean;
};

export function explainFailure(order: Pick<PaymentOrderResponse, "failedReasonCode" | "failedReasonMsg">): FailureExplanation {
  const code = order.failedReasonCode;
  if (code === null || code === undefined) {
    return { messageKey: "unknown", bankMessage: null, retryable: true };
  }
  if (code === -1) {
    return { messageKey: "providerRejected", bankMessage: null, retryable: true };
  }
  if (code === 0) {
    return { messageKey: "bank", bankMessage: order.failedReasonMsg?.trim() || null, retryable: true };
  }
  if (KNOWN_CODES.has(code)) {
    // 11 is PayTR's fraud flag: retrying is not the advice there.
    return { messageKey: String(code), bankMessage: null, retryable: code !== 11 };
  }
  return { messageKey: "unknown", bankMessage: null, retryable: true };
}
