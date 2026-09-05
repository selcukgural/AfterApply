import { z } from "zod";
import type { PasswordPolicy } from "@/types/api";
import { createPasswordSchema, type ValidationTranslator } from "./passwordPolicy";

export function createRegisterSchema(t: ValidationTranslator, policy: PasswordPolicy) {
  return z
    .object({
      email: z.string().min(1, t("emailRequired")).email(t("emailInvalid")),
      password: createPasswordSchema(policy, t),
      // Typed a second time so a typo in the (masked) password field is caught before the
      // account is created with a password the user can't reproduce.
      confirmPassword: z.string().min(1, t("passwordRequired")),
      firstName: z.string().min(1, t("firstNameRequired")).max(100),
      lastName: z.string().min(1, t("lastNameRequired")).max(100),
      consentAccepted: z.literal(true, { message: t("consentRequired") }),
    })
    .refine((values) => values.password === values.confirmPassword, {
      message: t("passwordsDoNotMatch"),
      path: ["confirmPassword"],
    });
}

export type RegisterFormValues = z.infer<ReturnType<typeof createRegisterSchema>>;
