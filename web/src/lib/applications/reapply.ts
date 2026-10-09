import type { ReapplyReminderResponse } from "@/types/api";
import { safeExternalUrl } from "@/lib/url/externalLink";

/** Mirrors ReapplyReminders.AllowedMonths on the server. */
export const REAPPLY_MONTHS = [3, 6, 12] as const;

/**
 * Where "look at their openings" goes on a due reminder: the company's LinkedIn jobs tab when its
 * page is known, else its website, else its page here. Only http(s) addresses leave the site —
 * both come from enrichment of third-party pages.
 */
export function reapplyOpeningsLink(reminder: Pick<ReapplyReminderResponse, "companyLinkedInUrl" | "companyWebsite" | "companySlug">):
  | { kind: "external"; href: string }
  | { kind: "internal"; href: string }
  | null {
  const linkedInJobs = linkedInCompanyJobsUrl(reminder.companyLinkedInUrl);
  if (linkedInJobs) return { kind: "external", href: linkedInJobs };
  const website = safeExternalUrl(reminder.companyWebsite);
  if (website) return { kind: "external", href: website };
  return reminder.companySlug ? { kind: "internal", href: `/companies/${reminder.companySlug}` } : null;
}

/**
 * The jobs tab of a LinkedIn company page, or null when the address is not one. The host is
 * checked on the parsed URL, not by pattern-matching the string: "https://evil.example/linkedin.com/company/x"
 * contains the right text but would send a link labelled as LinkedIn somewhere else.
 */
function linkedInCompanyJobsUrl(url: string | null | undefined): string | null {
  const safe = safeExternalUrl(url);
  if (!safe) return null;

  const parsed = new URL(safe);
  const host = parsed.hostname.toLowerCase();
  if (parsed.protocol !== "https:" || (host !== "linkedin.com" && !host.endsWith(".linkedin.com"))) return null;

  const company = /^\/company\/([^/]+)/i.exec(parsed.pathname);
  return company ? `${parsed.origin}/company/${company[1]}/jobs/` : null;
}
