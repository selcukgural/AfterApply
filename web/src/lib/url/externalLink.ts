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
