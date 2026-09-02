import { z } from "zod";

export function createResetPasswordSchema(t: (key: string) => string) {
  return z
    .object({
      newPassword: z.string().min(8, t("passwordMinLength")),
      confirmPassword: z.string().min(1, t("passwordRequired")),
    })
    .refine((values) => values.newPassword === values.confirmPassword, {
      message: t("passwordsDoNotMatch"),
      path: ["confirmPassword"],
    });
}

export type ResetPasswordFormValues = z.infer<ReturnType<typeof createResetPasswordSchema>>;
