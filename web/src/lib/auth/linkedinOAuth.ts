// The browser half of "Sign in with LinkedIn": OpenID Connect authorization-code flow, driven by
// a plain top-level redirect to linkedin.com — no LinkedIn script on the page, so the CSP in
// next.config.ts stays as tight as it is. The API does the code exchange (it holds the client
// secret) and verifies the ID token's signature against LinkedIn's JWKS.
//
// Unlike googleOAuth.ts there is no PKCE: LinkedIn's authorization/token endpoints don't take a
// code_challenge/code_verifier from a confidential (client-secret-holding) client. The random
// `state` in sessionStorage is still the login-CSRF defence — an attacker can put a code+state of
// *their* LinkedIn session in the victim's address bar, but cannot put the matching state into
// the victim's sessionStorage, so the callback refuses it.

const STORAGE_KEY = "aa_linkedin_oauth";
const AUTHORIZE_URL = "https://www.linkedin.com/oauth/v2/authorization";

interface PendingLinkedInSignIn {
  state: string;
  redirectUri: string;
}

function base64Url(bytes: Uint8Array): string {
  let binary = "";
  bytes.forEach((byte) => (binary += String.fromCharCode(byte)));
  return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

function randomToken(byteLength: number): string {
  const bytes = new Uint8Array(byteLength);
  crypto.getRandomValues(bytes);
  return base64Url(bytes);
}

export function linkedInCallbackUri(locale: string): string {
  return `${window.location.origin}/${locale}/auth/linkedin/callback`;
}

/** Stores the state for this attempt and navigates to LinkedIn. Never resolves in practice — the
 * page is gone once the redirect starts. */
export function beginLinkedInSignIn(clientId: string, locale: string): void {
  const state = randomToken(16);
  const redirectUri = linkedInCallbackUri(locale);

  const pending: PendingLinkedInSignIn = { state, redirectUri };
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(pending));

  const params = new URLSearchParams({
    response_type: "code",
    client_id: clientId,
    redirect_uri: redirectUri,
    state,
    // Identity only — the three scopes of LinkedIn's self-serve "Sign In with LinkedIn using
    // OpenID Connect" product; nothing that would touch the member's connections, posts or feed.
    scope: "openid profile email",
  });

  // An external origin (linkedin.com), which the Next.js router can't navigate to — the lint rule
  // is about internal pages.
  // eslint-disable-next-line @next/next/no-location-assign-relative-destination
  window.location.assign(`${AUTHORIZE_URL}?${params.toString()}`);
}

/** Returns the pending attempt whose state matches, and clears it — it is single-use, like the
 * authorization code it goes with. Null means this callback did not start in this browser. */
export function consumeLinkedInSignIn(state: string | null): PendingLinkedInSignIn | null {
  const raw = sessionStorage.getItem(STORAGE_KEY);
  sessionStorage.removeItem(STORAGE_KEY);
  if (!raw || !state) return null;

  try {
    const pending = JSON.parse(raw) as PendingLinkedInSignIn;
    return pending.state === state ? pending : null;
  } catch {
    return null;
  }
}
