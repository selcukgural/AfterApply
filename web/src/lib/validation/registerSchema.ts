import { z } from "zod";
import type { PasswordPolicy } from "@/types/api";
import { createPasswordSchema, type ValidationTranslator } from "./passwordPolicy";

export function createRegisterSchema(t: ValidationTranslator, policy: PasswordPolicy) {
  return z.object({
    email: z.string().min(1, t("emailRequired")).email(t("emailInvalid")),
    password: createPasswordSchema(policy, t),
    firstName: z.string().min(1, t("firstNameRequired")).max(100),
    lastName: z.string().min(1, t("lastNameRequired")).max(100),
    consentAccepted: z.literal(true, { message: t("consentRequired") }),
  });
}

export type RegisterFormValues = z.infer<ReturnType<typeof createRegisterSchema>>;
