import { z } from "zod";
import type { ValidationTranslator } from "./passwordPolicy";

// The complete-your-sign-up form after a first GitHub sign-in: the register schema minus the
// password (there is none). Email is GitHub's own, read-only, when GitHub exposed a verified and
// deliverable one — a GitHub account can keep every address private, so when it is missing the form
// has to collect one, and only then is it validated here.
export function createGitHubSignupSchema(t: ValidationTranslator, requiresEmail: boolean) {
  return z.object({
    email: requiresEmail
      ? z.string().min(1, t("emailRequired")).email(t("emailInvalid"))
      : z.string().optional(),
    firstName: z.string().min(1, t("firstNameRequired")).max(100),
    lastName: z.string().min(1, t("lastNameRequired")).max(100),
    consentAccepted: z.literal(true, { message: t("consentRequired") }),
  });
}

export type GitHubSignupFormValues = z.infer<ReturnType<typeof createGitHubSignupSchema>>;
