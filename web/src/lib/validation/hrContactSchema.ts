import { z } from "zod";
import { isLinkedInProfileUrl, safeMailtoUrl } from "@/lib/url/externalLink";

/**
 * The three HR-contact fields, shared by the application and tracked-job schemas so both agree
 * with each other and with HrContactRules on the backend. All optional: most applications never
 * have a contact, and clearing a field has to stay legal.
 *
 * The email check reuses safeMailtoUrl rather than a second regex — if a value would not survive
 * being turned into a mailto: link, accepting it here would only produce a field the detail page
 * silently refuses to render.
 */
export function hrContactSchemaFields(t: (key: string) => string) {
  return {
    hrName: z.string().max(200).optional(),
    hrEmail: z
      .string()
      .max(320)
      .optional()
      .refine((value) => !value || safeMailtoUrl(value) !== null, { message: t("hrEmailInvalid") }),
    hrLinkedInUrl: z
      .string()
      .max(500)
      .optional()
      .refine((value) => !value || isLinkedInProfileUrl(value), { message: t("hrLinkedInInvalid") }),
  };
}
