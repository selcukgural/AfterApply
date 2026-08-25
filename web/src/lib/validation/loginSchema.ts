import { z } from "zod";

export function createLoginSchema(t: (key: string) => string) {
  return z.object({
    email: z.string().min(1, t("emailRequired")).email(t("emailInvalid")),
    password: z.string().min(1, t("passwordRequired")),
  });
}

export type LoginFormValues = z.infer<ReturnType<typeof createLoginSchema>>;
