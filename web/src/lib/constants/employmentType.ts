import type { EmploymentType } from "@/types/api";

export const EMPLOYMENT_TYPES: EmploymentType[] = [
  "FullTime",
  "PartTime",
  "Contract",
  "Internship",
  "Freelance",
  "Temporary",
];

export const EMPLOYMENT_TYPE_LABELS: Record<EmploymentType, string> = {
  FullTime: "Tam Zamanlı",
  PartTime: "Yarı Zamanlı",
  Contract: "Sözleşmeli",
  Internship: "Staj",
  Freelance: "Serbest Çalışma",
  Temporary: "Geçici",
};
