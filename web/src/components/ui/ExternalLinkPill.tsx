import { safeExternalUrl, safeMailtoUrl } from "@/lib/url/externalLink";

type Icon = "globe" | "linkedin" | "mail";

const ICONS: Record<Icon, React.ReactNode> = {
  globe: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true" className="size-3.5 shrink-0">
      <circle cx="12" cy="12" r="9" />
      <path d="M3 12h18M12 3a15 15 0 0 1 0 18a15 15 0 0 1 0-18" />
    </svg>
  ),
  linkedin: (
    <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true" className="size-3.5 shrink-0">
      <path d="M4.98 3.5a2.5 2.5 0 1 1 0 5a2.5 2.5 0 0 1 0-5M3 9h4v12H3zM10 9h3.8v1.7h.05c.53-1 1.83-2.05 3.76-2.05C21.4 8.65 22 11.1 22 14.3V21h-4v-6c0-1.5-.03-3.4-2.07-3.4c-2.07 0-2.39 1.62-2.39 3.3V21h-4z" />
    </svg>
  ),
  mail: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true" className="size-3.5 shrink-0">
      <rect x="2.5" y="5" width="19" height="14" rx="2" />
      <path d="m3 7 9 6 9-6" />
    </svg>
  ),
};

interface ExternalLinkPillProps {
  /** Raw stored value — may be null, and may be anything a scraper or a user put there. */
  href: string | null | undefined;
  label: string;
  icon: Icon;
  /** Screen-reader text saying what the link is, since the visible label is often just a host. */
  title: string;
  /** How to read `href`: a web address, or an email address to build a mailto: from. */
  kind?: "url" | "email";
}

/**
 * Renders nothing at all when the value is missing or does not survive its gate — an empty row
 * beats a link that does something unexpected. Both gates live here rather than at the call sites
 * so that "put a stored value in an anchor" has exactly one place it can happen; see
 * safeExternalUrl and safeMailtoUrl for what each rejects.
 */
export function ExternalLinkPill({ href, label, icon, title, kind = "url" }: ExternalLinkPillProps) {
  const safeHref = kind === "email" ? safeMailtoUrl(href) : safeExternalUrl(href);
  if (!safeHref) {
    return null;
  }

  // A mailto: hands off to the user's mail client; opening it in a new tab would leave a blank one
  // behind, and rel/target are meaningless for it.
  const isMail = kind === "email";

  return (
    <a
      href={safeHref}
      target={isMail ? undefined : "_blank"}
      rel={isMail ? undefined : "noreferrer"}
      title={title}
      className="inline-flex max-w-full items-center gap-1.5 rounded-full border border-gray-200 bg-white px-2.5 py-1 text-xs text-gray-700 transition-colors hover:border-accent hover:text-accent dark:border-gray-700 dark:bg-gray-900 dark:text-gray-300 dark:hover:border-accent"
    >
      {ICONS[icon]}
      <span className="truncate">{label}</span>
    </a>
  );
}
