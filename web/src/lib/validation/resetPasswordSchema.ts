import { z } from "zod";
import type { PasswordPolicy } from "@/types/api";
import { createPasswordSchema, type ValidationTranslator } from "./passwordPolicy";

export function createResetPasswordSchema(t: ValidationTranslator, policy: PasswordPolicy) {
  return z
    .object({
      newPassword: createPasswordSchema(policy, t),
      confirmPassword: z.string().min(1, t("passwordRequired")),
    })
    .refine((values) => values.newPassword === values.confirmPassword, {
      message: t("passwordsDoNotMatch"),
      path: ["confirmPassword"],
    });
}

export type ResetPasswordFormValues = z.infer<ReturnType<typeof createResetPasswordSchema>>;
