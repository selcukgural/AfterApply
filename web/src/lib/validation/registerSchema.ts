import { z } from "zod";

export function createRegisterSchema(t: (key: string) => string) {
  return z.object({
    email: z.string().min(1, t("emailRequired")).email(t("emailInvalid")),
    password: z.string().min(8, t("passwordMinLength")),
    firstName: z.string().min(1, t("firstNameRequired")).max(100),
    lastName: z.string().min(1, t("lastNameRequired")).max(100),
    consentAccepted: z.literal(true, { message: t("consentRequired") }),
  });
}

export type RegisterFormValues = z.infer<ReturnType<typeof createRegisterSchema>>;
