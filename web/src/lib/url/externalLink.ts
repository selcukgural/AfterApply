/**
 * Every URL rendered as an outbound link came from outside this app — scraped from a job page by
 * the extension, read off a company profile by CompanyEnrichmentService, or typed into a form —
 * so none of it may go straight into an `href`. `javascript:` and `data:` are perfectly valid
 * URLs that the backend's "is this a web URL" checks do not all reject, and putting one in an
 * anchor turns a stored string into script the moment a user clicks it. Anything that is not
 * plain http(s) comes back null here, and the caller renders no link at all rather than a broken
 * or dangerous one.
 */
export function safeExternalUrl(url: string | null | undefined): string | null {
  if (!url) {
    return null;
  }

  let parsed: URL;
  try {
    parsed = new URL(url);
  } catch {
    // Not absolute (or not a URL at all). A relative href here would point back at our own app,
    // which is never what an "open the company's site" link means.
    return null;
  }

  return parsed.protocol === "http:" || parsed.protocol === "https:" ? parsed.href : null;
}

/**
 * What to show for a link whose full URL is noise: "https://www.oplog.com/careers?ref=x" reads as
 * "oplog.com". Falls back to the raw string when it cannot be parsed, so a label is never empty —
 * this is display only, and callers still gate the href itself through safeExternalUrl.
 */
export function externalUrlLabel(url: string): string {
  try {
    return new URL(url).hostname.replace(/^www\./, "");
  } catch {
    return url;
  }
}

/**
 * A LinkedIn *profile* URL, which is what the HR-contact field means — the UI labels it that way
 * and renders it behind a LinkedIn mark, so an arbitrary link would be a mark that lies about
 * where it goes. Mirrors HrContactRules.BeALinkedInProfileUrl on the backend; both exist because
 * either side alone would let the other's callers through.
 */
export function isLinkedInProfileUrl(url: string): boolean {
  let parsed: URL;
  try {
    parsed = new URL(url);
  } catch {
    return false;
  }

  const host = parsed.hostname.toLowerCase();
  return (
    parsed.protocol === "https:" &&
    (host === "linkedin.com" || host.endsWith(".linkedin.com")) &&
    parsed.pathname.toLowerCase().startsWith("/in/")
  );
}

/**
 * A "mail this person" href. Same reasoning as safeExternalUrl: the address is user-entered and
 * ends up in an anchor, so anything that is not a plain single address is dropped rather than
 * interpolated — a value containing a newline or a second address could otherwise smuggle extra
 * mailto parameters (a bcc, a body) into the link.
 */
export function safeMailtoUrl(email: string | null | undefined): string | null {
  if (!email) {
    return null;
  }

  // Deliberately stricter than "is this a valid address": the characters excluded here are the
  // ones that would change the meaning of the href rather than the address — whitespace and
  // separators (a second recipient), and "?" / "&" / "#" (mailto headers such as bcc or body).
  // Percent-encoding the result instead would turn the "@" into %40, which strict mail clients
  // are within their rights to reject, so the address is kept literal and the input narrowed.
  const trimmed = email.trim();
  const address = /^[^\s@,;:<>()[\]\\?&#%"']+@[^\s@,;:<>()[\]\\?&#%"']+\.[^\s@,;:<>()[\]\\?&#%"']+$/;
  return address.test(trimmed) ? `mailto:${trimmed}` : null;
}
