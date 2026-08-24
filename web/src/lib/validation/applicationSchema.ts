import { z } from "zod";

const applicationBaseSchema = z.object({
  jobTitle: z.string().min(1, "Pozisyon adı gerekli").max(300),
  jobUrl: z.string().max(2000).optional(),
  location: z.string().max(200).optional(),
  employmentType: z.string().min(1, "Çalışma şekli gerekli"),
  appliedAt: z.string().min(1, "Başvuru tarihi gerekli"),
  notes: z.string().max(4000).optional(),
});

export const createApplicationSchema = applicationBaseSchema.extend({
  companyName: z.string().min(1, "Şirket adı gerekli").max(300),
  source: z.string().optional(),
});

export const updateApplicationSchema = applicationBaseSchema;

export type CreateApplicationFormValues = z.infer<typeof createApplicationSchema>;
export type UpdateApplicationFormValues = z.infer<typeof updateApplicationSchema>;
