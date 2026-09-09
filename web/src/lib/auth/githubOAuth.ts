// The browser half of "Sign in with GitHub": OAuth authorization-code flow, driven by a plain
// top-level redirect to github.com — no GitHub script on the page, so the CSP in next.config.ts
// stays as tight as it is. The API does the code exchange (it holds the client secret) and reads
// the identity from api.github.com.
//
// Like linkedinOAuth.ts and unlike googleOAuth.ts there is no PKCE: GitHub's OAuth App endpoints
// don't take a code_challenge/code_verifier. The random `state` in sessionStorage is the login-CSRF
// defence — an attacker can put a code+state of *their* GitHub session in the victim's address bar,
// but cannot put the matching state into the victim's sessionStorage, so the callback refuses it.

const STORAGE_KEY = "aa_github_oauth";
const AUTHORIZE_URL = "https://github.com/login/oauth/authorize";

interface PendingGitHubSignIn {
  state: string;
  redirectUri: string;
  /** Same role as the Google flow's field of the same name — see googleOAuth.ts. */
  returnTo?: string;
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

export function gitHubCallbackUri(locale: string): string {
  return `${window.location.origin}/${locale}/auth/github/callback`;
}

/** Stores the state for this attempt and navigates to GitHub. Never resolves in practice — the
 * page is gone once the redirect starts. */
export function beginGitHubSignIn(clientId: string, locale: string, returnTo?: string | null): void {
  const state = randomToken(16);
  const redirectUri = gitHubCallbackUri(locale);

  const pending: PendingGitHubSignIn = { state, redirectUri, ...(returnTo ? { returnTo } : {}) };
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(pending));

  const params = new URLSearchParams({
    client_id: clientId,
    redirect_uri: redirectUri,
    state,
    // Identity only: read:user is the profile, user:email is the address list (GitHub exposes no
    // email at all without it). Neither can read a repository, private or public.
    scope: "read:user user:email",
  });

  // An external origin (github.com), which the Next.js router can't navigate to — the lint rule
  // is about internal pages.
  // eslint-disable-next-line @next/next/no-location-assign-relative-destination
  window.location.assign(`${AUTHORIZE_URL}?${params.toString()}`);
}

/** Returns the pending attempt whose state matches, and clears it — it is single-use, like the
 * authorization code it goes with. Null means this callback did not start in this browser. */
export function consumeGitHubSignIn(state: string | null): PendingGitHubSignIn | null {
  const raw = sessionStorage.getItem(STORAGE_KEY);
  sessionStorage.removeItem(STORAGE_KEY);
  if (!raw || !state) return null;

  try {
    const pending = JSON.parse(raw) as PendingGitHubSignIn;
    return pending.state === state ? pending : null;
  } catch {
    return null;
  }
}
