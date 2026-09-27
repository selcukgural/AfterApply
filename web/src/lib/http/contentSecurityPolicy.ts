// The site's Content-Security-Policy, built per request by proxy.ts (DECISIONS.md 2026-09-27).
//
// script-src carries a fresh nonce instead of 'unsafe-inline'. Next.js reads the nonce off the
// request's CSP header and stamps it on its own bootstrap/RSC inline scripts and chunk tags; the
// root layout stamps it on the theme boot script. 'strict-dynamic' lets a nonced script load
// further scripts (next/script, lazy chunks) without listing them; with it, browsers ignore 'self'
// in script-src, which stays only as a fallback for ones that predate CSP3. An injected inline
// script or a script tag written into the page has no nonce, so it does not run.
//
// A nonce only means something if it is new on every response, which is why every page is
// rendered per request since this change: a prerendered page would carry the build's nonce, or
// none, and its scripts would be refused.
//
// The other directives are ordered by what they buy:
//  - connect-src bounds where injected script could send what it reads: our own API and Sentry.
//  - frame-ancestors closes clickjacking on the state-changing screens (suggestion confirm,
//    account deletion).
//  - base-uri stops an injected <base> tag from repointing every relative script URL.
//  - object-src/form-action remove two more legacy escape hatches.
//
// style-src keeps 'unsafe-inline': React's style attributes and the libraries' injected styles
// have no nonce path, and an injected style can restyle a page but not run code.
//
// The PayTR checkout is the one deliberate exception (DECISIONS.md 2026-09-15):
//  - frame-src https://www.paytr.com is in the *global* policy, because the checkout is reached by
//    a client-side navigation and a CSP belongs to the document, not the route.
//  - script-src does NOT widen: PayTR's iframe-resizer parent script is served from our own
//    origin (public/vendor/paytr-iframeResizer.min.js) and inserted by next/script, which
//    'strict-dynamic' covers.
//  - /{locale}/pro/return/* may be rendered inside that frame when PayTR navigates it to our
//    merchant_ok_url, so that route alone allows paytr.com as a frame ancestor (next.config.ts
//    also drops X-Frame-Options there).

export const PAYTR_ORIGIN = "https://www.paytr.com";

const PAYTR_RETURN_PATH = /^\/(?:tr|en)\/pro\/return\//;

export interface CspOrigins {
  apiOrigin: string;
  sentryOrigin: string | null;
}

export function originOf(value: string | undefined): string | null {
  if (!value) return null;
  try {
    return new URL(value).origin;
  } catch {
    return null;
  }
}

// Both are baked in at build time (see web/Dockerfile), so the policy is derived from them rather
// than hardcoding a production hostname that would silently break local/preview builds.
export function cspOriginsFromEnv(): CspOrigins {
  return {
    apiOrigin: originOf(process.env.NEXT_PUBLIC_API_BASE_URL) ?? "http://localhost:5151",
    sentryOrigin: originOf(process.env.NEXT_PUBLIC_SENTRY_DSN),
  };
}

/** 128 random bits, base64 — unguessable and new for every response. */
export function createNonce(): string {
  const bytes = new Uint8Array(16);
  crypto.getRandomValues(bytes);
  return btoa(String.fromCharCode(...bytes));
}

export function buildCsp(options: { nonce: string; pathname: string; origins: CspOrigins; isDev: boolean }): string {
  const { nonce, pathname, origins, isDev } = options;
  // SignalR (/hubs/import-progress) upgrades to a WebSocket against the API's origin, and ws:/wss:
  // are their own CSP scheme — the https origin does not cover them.
  const apiWebSocketOrigin = origins.apiOrigin.replace(/^http/, "ws");

  const directives: Record<string, string> = {
    "default-src": "'self'",
    // React reconstructs server error stacks with eval() in development only.
    "script-src": `'self' 'nonce-${nonce}' 'strict-dynamic'${isDev ? " 'unsafe-eval'" : ""}`,
    "style-src": "'self' 'unsafe-inline'",
    "img-src": "'self' data: blob:",
    "font-src": "'self' data:",
    "frame-src": PAYTR_ORIGIN,
    "connect-src": `'self' ${origins.apiOrigin} ${apiWebSocketOrigin}${origins.sentryOrigin ? ` ${origins.sentryOrigin}` : ""}`,
    "frame-ancestors": PAYTR_RETURN_PATH.test(pathname) ? `'self' ${PAYTR_ORIGIN}` : "'none'",
    "base-uri": "'self'",
    "form-action": "'self'",
    "object-src": "'none'",
  };

  return Object.entries(directives)
    .map(([key, value]) => `${key} ${value}`)
    .join("; ");
}
