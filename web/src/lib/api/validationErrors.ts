import { ApiError } from "./httpClient";

/**
 * The per-field messages of an ASP.NET validation problem (`errors: { Field: [msg] }`), keyed the
 * way the request's TypeScript type spells the field ("BillingName" → "billingName"). Empty for
 * anything that is not a validation problem, so a caller can fall back to the flat message.
 */
export function fieldErrorsOf(error: unknown): Record<string, string> {
  if (!(error instanceof ApiError) || !error.body || typeof error.body !== "object" || !("errors" in error.body)) {
    return {};
  }
  const errors = (error.body as { errors: unknown }).errors;
  if (!errors || typeof errors !== "object") {
    return {};
  }
  const result: Record<string, string> = {};
  for (const [field, messages] of Object.entries(errors as Record<string, unknown>)) {
    const first = Array.isArray(messages) ? messages.find((item): item is string => typeof item === "string") : undefined;
    if (first) {
      result[field.charAt(0).toLowerCase() + field.slice(1)] = first;
    }
  }
  return result;
}
