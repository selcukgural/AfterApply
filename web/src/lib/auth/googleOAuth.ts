// The browser half of "Sign in with Google": authorization-code flow with PKCE, driven by a plain
// top-level redirect to accounts.google.com — no Google script on the page, so the CSP in
// next.config.ts stays as tight as it is. The API does the code exchange (it holds the client
// secret); this module only produces what that exchange needs and proves the round-trip started
// in this same browser.
//
// The PKCE verifier and the state live in sessionStorage for the duration of the redirect. That
// is also the login-CSRF defence: an attacker can put a code+state of *their* Google session in
// the victim's address bar, but cannot put the matching verifier into the victim's sessionStorage,
// so the callback refuses it.

const STORAGE_KEY = "aa_google_oauth";
const AUTHORIZE_URL = "https://accounts.google.com/o/oauth2/v2/auth";

interface PendingGoogleSignIn {
  state: string;
  codeVerifier: string;
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

async function sha256(value: string): Promise<Uint8Array> {
  const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(value));
  return new Uint8Array(digest);
}

export function googleCallbackUri(locale: string): string {
  return `${window.location.origin}/${locale}/auth/google/callback`;
}

/** Stores the PKCE/state pair for this attempt and navigates to Google. Never resolves in
 * practice — the page is gone once the redirect starts. */
export async function beginGoogleSignIn(clientId: string, locale: string): Promise<void> {
  // 32 random bytes → 43 base64url chars, the RFC 7636 minimum.
  const codeVerifier = randomToken(32);
  const codeChallenge = base64Url(await sha256(codeVerifier));
  const state = randomToken(16);
  const redirectUri = googleCallbackUri(locale);

  const pending: PendingGoogleSignIn = { state, codeVerifier, redirectUri };
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(pending));

  const params = new URLSearchParams({
    client_id: clientId,
    redirect_uri: redirectUri,
    response_type: "code",
    // Identity only — nothing that would need Google's app verification or grant access to
    // any Google data.
    scope: "openid email profile",
    state,
    code_challenge: codeChallenge,
    code_challenge_method: "S256",
    // Always let the user pick the account; silently reusing the last one is how people end up
    // signed into the wrong account on a shared machine.
    prompt: "select_account",
  });

  // An external origin (accounts.google.com), which the Next.js router can't navigate to — the
  // lint rule is about internal pages.
  // eslint-disable-next-line @next/next/no-location-assign-relative-destination
  window.location.assign(`${AUTHORIZE_URL}?${params.toString()}`);
}

/** Returns the pending attempt whose state matches, and clears it — it is single-use, like the
 * authorization code it goes with. Null means this callback did not start in this browser. */
export function consumeGoogleSignIn(state: string | null): PendingGoogleSignIn | null {
  const raw = sessionStorage.getItem(STORAGE_KEY);
  sessionStorage.removeItem(STORAGE_KEY);
  if (!raw || !state) return null;

  try {
    const pending = JSON.parse(raw) as PendingGoogleSignIn;
    return pending.state === state ? pending : null;
  } catch {
    return null;
  }
}
