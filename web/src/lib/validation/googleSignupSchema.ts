import { z } from "zod";
import type { ValidationTranslator } from "./passwordPolicy";

// The complete-your-sign-up form after a first Google sign-in: the register schema minus
// email (Google's, read-only) and password (there is none).
export function createGoogleSignupSchema(t: ValidationTranslator) {
  return z.object({
    firstName: z.string().min(1, t("firstNameRequired")).max(100),
    lastName: z.string().min(1, t("lastNameRequired")).max(100),
    consentAccepted: z.literal(true, { message: t("consentRequired") }),
  });
}

export type GoogleSignupFormValues = z.infer<ReturnType<typeof createGoogleSignupSchema>>;
