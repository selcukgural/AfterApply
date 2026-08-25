import { z } from "zod";

export const registerSchema = z.object({
  email: z.string().min(1, "E-posta gerekli").email("Geçerli bir e-posta girin"),
  password: z.string().min(8, "Şifre en az 8 karakter olmalı"),
  firstName: z.string().min(1, "Ad gerekli").max(100),
  lastName: z.string().min(1, "Soyad gerekli").max(100),
  consentAccepted: z.literal(true, { message: "Gizlilik politikasını kabul etmelisiniz" }),
});

export type RegisterFormValues = z.infer<typeof registerSchema>;
