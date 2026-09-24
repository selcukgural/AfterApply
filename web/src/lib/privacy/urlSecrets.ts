/**
 * Query parameters that carry a credential or an address: the password-reset link's `token` and
 * `email`, an OAuth callback's `code` and `state`, the extension pairing `code`, a SignalR
 * `access_token`/ticket. A URL is copied into places nobody audits — error reports, breadcrumbs,
 * request logs, the Referer of the next request — so wherever we hand one on, these go first.
 */
export const SENSITIVE_QUERY_KEYS: readonly string[] = [
  "token",
  "access_token",
  "id_token",
  "ticket",
  "code",
  "state",
  "email",
];

const FILTERED = "[Filtered]";

function isSensitive(key: string): boolean {
  return SENSITIVE_QUERY_KEYS.includes(key.toLowerCase());
}

/** Scrubs one query string ("a=1&token=x", with or without its "?"). */
export function scrubQueryString(query: string): string {
  const hasMark = query.startsWith("?");
  const body = hasMark ? query.slice(1) : query;
  if (!body) return query;
  const scrubbed = body
    .split("&")
    .map((pair) => {
      const eq = pair.indexOf("=");
      const rawKey = eq === -1 ? pair : pair.slice(0, eq);
      let key = rawKey;
      try {
        key = decodeURIComponent(rawKey.replace(/\+/g, " "));
      } catch {
        // A malformed escape is still compared as typed.
      }
      return eq !== -1 && isSensitive(key) ? `${rawKey}=${FILTERED}` : pair;
    })
    .join("&");
  return hasMark ? `?${scrubbed}` : scrubbed;
}

/**
 * The same URL with every sensitive parameter's value replaced, in the query and in the fragment
 * (OAuth implicit flows put tokens after the "#"). Relative and absolute URLs alike; anything that
 * is not a string passes through untouched.
 */
export function scrubUrl<T>(url: T): T {
  if (typeof url !== "string" || (!url.includes("?") && !url.includes("#"))) return url;
  const hashAt = url.indexOf("#");
  const beforeHash = hashAt === -1 ? url : url.slice(0, hashAt);
  const hash = hashAt === -1 ? "" : url.slice(hashAt + 1);
  const queryAt = beforeHash.indexOf("?");
  const path = queryAt === -1 ? beforeHash : beforeHash.slice(0, queryAt);
  const query = queryAt === -1 ? "" : beforeHash.slice(queryAt);
  const scrubbedHash = hash.includes("=") ? `#${scrubQueryString(hash)}` : hashAt === -1 ? "" : `#${hash}`;
  return `${path}${scrubQueryString(query)}${scrubbedHash}` as T;
}

/**
 * Removes the given parameters from the address bar without a navigation. For a page that has
 * already read a secret out of its own URL (the reset link's token): once it is in component
 * state, the address bar, the history entry and every later Referer can do without it.
 */
export function removeQueryParamsFromAddressBar(keys: readonly string[]): void {
  if (typeof window === "undefined") return;
  const url = new URL(window.location.href);
  let changed = false;
  for (const key of keys) {
    if (url.searchParams.has(key)) {
      url.searchParams.delete(key);
      changed = true;
    }
  }
  if (changed) {
    window.history.replaceState(window.history.state, "", `${url.pathname}${url.search}${url.hash}`);
  }
}
