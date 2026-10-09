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
  const linkedIn = safeExternalUrl(reminder.companyLinkedInUrl);
  if (linkedIn && /linkedin\.com\/company\//i.test(linkedIn)) {
    return { kind: "external", href: `${linkedIn.replace(/[?#].*$/, "").replace(/\/+$/, "")}/jobs/` };
  }
  const website = safeExternalUrl(reminder.companyWebsite);
  if (website) return { kind: "external", href: website };
  return reminder.companySlug ? { kind: "internal", href: `/companies/${reminder.companySlug}` } : null;
}
