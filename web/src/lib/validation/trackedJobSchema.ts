import { z } from "zod";

export function createTrackedJobSchema(t: (key: string) => string) {
  return z.object({
    companyName: z.string().min(1, t("companyNameRequired")).max(300),
    jobTitle: z.string().min(1, t("jobTitleRequired")).max(300),
    jobUrl: z.string().max(2000).optional(),
    location: z.string().max(200).optional(),
    notes: z.string().max(4000).optional(),
  });
}

export type CreateTrackedJobFormValues = z.infer<ReturnType<typeof createTrackedJobSchema>>;
