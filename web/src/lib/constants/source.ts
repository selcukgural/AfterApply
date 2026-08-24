import type { Source } from "@/types/api";

export const SOURCES: Source[] = [
  "Manual",
  "LinkedIn",
  "LinkedInImport",
  "CsvImport",
  "CompanyWebsite",
  "Referral",
  "BrowserExtension",
  "Email",
  "System",
  "Other",
];

export const SOURCE_LABELS: Record<Source, string> = {
  Manual: "Elle Girildi",
  LinkedIn: "LinkedIn",
  LinkedInImport: "LinkedIn İçe Aktarma",
  CsvImport: "CSV İçe Aktarma",
  CompanyWebsite: "Şirket Web Sitesi",
  Referral: "Referans",
  BrowserExtension: "Tarayıcı Eklentisi",
  Email: "E-posta",
  System: "Sistem",
  Other: "Diğer",
};
