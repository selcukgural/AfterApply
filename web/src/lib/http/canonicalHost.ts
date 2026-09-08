/**
 * Making the apex host the only address.
 *
 * `www.ekariyerim.com` and `ekariyerim.com` are two Cloud Run domain mappings pointing at the same
 * service, so every page existed on two hosts. The canonical tag named the apex, which is enough for
 * Google to consolidate eventually, but it left the duplicate reachable — and reachable is what
 * matters: the sitemap lists only apex URLs, yet it was possible to submit and crawl it from the www
 * host, and a visitor landing on www stayed there.
 *
 * Cloudflare cannot fix this from the DNS side. The `www` record is deliberately DNS-only (a CNAME
 * straight to ghs.googlehosted.com so Cloud Run can terminate TLS for it), so nothing of
 * Cloudflare's sits in the request path and its redirect rules never see the request.
 */

const WWW_PREFIX = "www.";

/**
 * The apex URL a request should be sent to, or null when it is already on the canonical host.
 *
 * `host` comes from the Host header rather than the parsed request URL: behind Cloud Run's frontend
 * the URL can carry an internal host, while the header is what the visitor actually typed.
 *
 * The result is always https and carries no port: the only host this ever rewrites is the public
 * one, and a www request arriving over http should still land on the secure apex rather than take a
 * second redirect to get there.
 */
export function apexRedirectUrl(
  host: string | null | undefined,
  pathname: string,
  search: string,
): string | null {
  if (!host) return null;

  const hostname = host.split(":")[0].toLowerCase();
  if (!hostname.startsWith(WWW_PREFIX)) return null;

  const apex = hostname.slice(WWW_PREFIX.length);
  if (!apex) return null;

  return `https://${apex}${pathname}${search}`;
}

/**
 * A request for a file rather than a page — /sitemap.xml and /robots.txt.
 *
 * Those two reach the proxy only so the redirect above covers them as well; the locale middleware
 * must not see them, or it would rewrite /sitemap.xml to /tr/sitemap.xml and Google would fetch a
 * 404 from the URL robots.txt advertises.
 */
export function isFileRequest(pathname: string): boolean {
  return /\/[^/]+\.[^/]+$/.test(pathname);
}
