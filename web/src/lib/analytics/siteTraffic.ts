import { API_BASE_URL } from "@/lib/api/httpClient";

/**
 * The public site's visit counter.
 *
 * Three rules hold this feature together, and all three live in this file:
 *
 * 1. **It never goes through `apiFetch`.** `apiFetch` attaches the signed-in visitor's
 *    Authorization header and refreshes it on 401 — both of which would tie a page view to an
 *    account. The counter is anonymous by construction, not by policy, so it talks to the API with
 *    a bare `fetch` that carries no credentials at all.
 * 2. **Nothing identifying leaves the browser.** The path is sent without its query string and the
 *    referrer is reduced to an origin here, before the request is made — so a password-reset token
 *    or a search term never travels, rather than travelling and being discarded server-side. The
 *    API strips both again; this is the first of the two passes, not a substitute for it.
 * 3. **It can never break a page.** Every failure is swallowed. A counter that takes the site down
 *    with it would be worse than no counter.
 *
 * There is no cookie, no localStorage, no visitor id and no session id — which is what lets the
 * site keep the promise on /cookies that it carries no tracking. See the API's
 * SiteTrafficDailyCounter for what a stored row does and does not contain.
 */

export type SiteTrafficEvent =
  | "page_view"
  | "cta_get_started"
  | "cv_scan_completed"
  | "register_started"
  | "register_completed";

export interface SiteTrafficPayload {
  event: SiteTrafficEvent;
  path: string;
  referrer: string | null;
}

/**
 * Do Not Track is deprecated and honoured by almost nothing, and a counter that stores no
 * identifier arguably falls outside what it was meant to stop. Respected anyway: it costs one
 * comparison, and a product whose pitch is "we do not track you" should not be the one arguing
 * about the fine print.
 */
export function isDoNotTrackEnabled(value: string | null | undefined): boolean {
  return value === "1" || value === "yes";
}

/**
 * Everything the browser is allowed to say about a visit — kept pure so the two rules that matter
 * can be tested without a DOM: the query string is cut off the path, and a referrer is reduced to
 * a bare origin.
 */
export function buildSiteTrafficPayload(
  event: SiteTrafficEvent,
  rawPath: string,
  rawReferrer: string | null | undefined,
): SiteTrafficPayload {
  return {
    event,
    // Split on both, in this order: a fragment can follow a query and a query can follow a
    // fragment in a malformed URL, and neither may survive.
    path: rawPath.split("?")[0].split("#")[0],
    referrer: originOf(rawReferrer),
  };
}

/** A referrer reduced to its origin, or null. Never the referring path or its query. */
function originOf(referrer: string | null | undefined): string | null {
  if (!referrer) {
    return null;
  }
  try {
    const url = new URL(referrer);
    if (url.protocol !== "http:" && url.protocol !== "https:") {
      return null;
    }
    return url.origin;
  } catch {
    return null;
  }
}

function doNotTrackValue(): string | null | undefined {
  if (typeof navigator === "undefined") {
    return undefined;
  }
  return (
    navigator.doNotTrack ??
    (window as unknown as { doNotTrack?: string }).doNotTrack ??
    (navigator as unknown as { msDoNotTrack?: string }).msDoNotTrack
  );
}

export function trackSiteTraffic(event: SiteTrafficEvent, path?: string): void {
  if (typeof window === "undefined" || isDoNotTrackEnabled(doNotTrackValue())) {
    return;
  }

  // Default to the pathname only — never location.href, which carries the query.
  const payload = buildSiteTrafficPayload(
    event,
    path ?? window.location.pathname,
    typeof document === "undefined" ? null : document.referrer,
  );

  try {
    void fetch(`${API_BASE_URL}/api/site-traffic/events`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      // keepalive so a report fired as the visitor leaves the page still completes.
      keepalive: true,
      body: JSON.stringify(payload),
      // No cookies, no Authorization header: this request must be indistinguishable from any
      // other visitor's.
      credentials: "omit",
    }).catch(() => {
      // Offline, blocked by an extension, rate limited — all fine, all silent.
    });
  } catch {
    // fetch itself throwing (very old browsers, locked-down environments) is equally fine.
  }
}
