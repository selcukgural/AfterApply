import { z } from "zod";

export function createForgotPasswordSchema(t: (key: string) => string) {
  return z.object({
    email: z.string().min(1, t("emailRequired")).email(t("emailInvalid")),
  });
}

export type ForgotPasswordFormValues = z.infer<ReturnType<typeof createForgotPasswordSchema>>;
