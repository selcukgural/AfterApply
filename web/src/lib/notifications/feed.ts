import type { ContributionNotificationResponse, NotificationFeedItemResponse } from "@/types/api";

/** Where a contribution row leads: the company page on the tab the contribution sits in, or the
 *  blog post's comments in the post's own language (a Turkish post has no English URL). */
export function contributionTarget(c: ContributionNotificationResponse): { href: string; locale?: string } | null {
  switch (c.type) {
    case "ReviewHelpful":
      return c.companySlug ? { href: `/companies/${c.companySlug}?tab=reviews` } : null;
    case "SalaryHelpful":
      return c.companySlug ? { href: `/companies/${c.companySlug}?tab=salaries` } : null;
    case "ExperienceHelpful":
      return c.companySlug ? { href: `/companies/${c.companySlug}?tab=experiences` } : null;
    case "BlogCommentHelpful":
      return c.blogPostSlug ? { href: `/blog/${c.blogPostSlug}#comments`, locale: c.blogPostLanguage ?? undefined } : null;
    default:
      return null;
  }
}

/** Where a Gmail row leads: its application, when it has one. */
export function emailTarget(item: NotificationFeedItemResponse): string | null {
  return item.email?.applicationId ? `/applications/${item.email.applicationId}` : null;
}

/** "23 September 2026, 14:32" — the date and the time, as the user asked for on every row. */
export function formatNotificationTime(iso: string, locale: string): string {
  return new Intl.DateTimeFormat(locale, { dateStyle: "long", timeStyle: "short" }).format(new Date(iso));
}
