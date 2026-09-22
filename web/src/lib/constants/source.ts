import type { Source } from "@/types/api";

/** The channels a person can pick when entering an application by hand — Application.source, not
 * Job.source. The ATS members of the Source union (Greenhouse, Lever, Workday, ...) are deliberately
 * absent: those describe where a posting was read from and are only ever set by the backend's
 * JobPostingSourceResolver, never chosen in this form. */
export const SOURCES: Source[] = [
  "Manual",
  "LinkedIn",
  "KariyerNet",
  "LinkedInImport",
  "CsvImport",
  "CompanyWebsite",
  "Referral",
  "BrowserExtension",
  "Email",
  "System",
  "Other",
];
