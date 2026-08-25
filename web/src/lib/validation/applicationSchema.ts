import { z } from "zod";

function createApplicationBaseSchema(t: (key: string) => string) {
  return z.object({
    jobTitle: z.string().min(1, t("jobTitleRequired")).max(300),
    jobUrl: z.string().max(2000).optional(),
    location: z.string().max(200).optional(),
    employmentType: z.string().min(1, t("employmentTypeRequired")),
    appliedAt: z.string().min(1, t("appliedAtRequired")),
    notes: z.string().max(4000).optional(),
  });
}

export function createApplicationSchema(t: (key: string) => string) {
  return createApplicationBaseSchema(t).extend({
    companyName: z.string().min(1, t("companyNameRequired")).max(300),
    source: z.string().optional(),
  });
}

export function createUpdateApplicationSchema(t: (key: string) => string) {
  return createApplicationBaseSchema(t);
}

export type CreateApplicationFormValues = z.infer<ReturnType<typeof createApplicationSchema>>;
export type UpdateApplicationFormValues = z.infer<ReturnType<typeof createUpdateApplicationSchema>>;
