import { z } from "zod";
import type { ValidationTranslator } from "./passwordPolicy";

// The complete-your-sign-up form after a first LinkedIn sign-in: the register schema minus the
// password (there is none). Email is LinkedIn's own, read-only, when LinkedIn provided a verified
// one — but LinkedIn's OpenID Connect response makes it optional, so when it is missing the form
// has to collect one, and only then is it validated here.
export function createLinkedInSignupSchema(t: ValidationTranslator, requiresEmail: boolean) {
  return z.object({
    email: requiresEmail
      ? z.string().min(1, t("emailRequired")).email(t("emailInvalid"))
      : z.string().optional(),
    firstName: z.string().min(1, t("firstNameRequired")).max(100),
    lastName: z.string().min(1, t("lastNameRequired")).max(100),
    consentAccepted: z.literal(true, { message: t("consentRequired") }),
  });
}

export type LinkedInSignupFormValues = z.infer<ReturnType<typeof createLinkedInSignupSchema>>;
