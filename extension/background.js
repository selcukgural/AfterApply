// MV3 background service worker. It exists for exactly one reason: to make the API calls that the
// Gmail content scripts (gmail-scan.js, local-filter-config.js) cannot make themselves.
//
// A content script's fetch() is issued on behalf of the page it was injected into — mail.google.com
// here — and is therefore subject to that page's CORS, which host_permissions does NOT exempt it
// from ("Cross-origin requests are always treated as such in content scripts, even if the extension
// has host permissions", Chrome's own network-requests doc). The API's CORS allow-list contains the
// e-kariyerim web origin and nothing else, so every request gmail-scan.js used to make from
// mail.google.com was blocked by the browser before it ever reached the server, and the empty catch
// around it turned that into silence: no suggestion, no error, nothing. A service worker runs on the
// extension's own origin, where host_permissions does apply and CORS does not — so the calls are
// made from here and the result is messaged back.
//
// Plain script, not an ES module (no `"type": "module"` in the manifest) so it stays consistent with
// the content scripts it serves; it duplicates the two storage keys it needs rather than importing
// storage.js, same reason those scripts do.

const AFTERAPPLY_SETTINGS_KEY = "afterapply_settings"; // must match storage.js's STORAGE_KEY
const AFTERAPPLY_DEFAULT_API_BASE_URL = "https://api.ekariyerim.com";

/** Message type the content scripts send. Namespaced so it can never collide with a message from
 * anything else sharing this extension's runtime. */
const AFTERAPPLY_API_MESSAGE = "afterapply:api";

// Fixed route table, keyed by name. The caller names a route; it never supplies a path, a method or
// a host. A content script runs in a page the user could have been lured to, so treating its message
// as data — never as "fetch whatever this says" — is what keeps this worker from becoming an open
// proxy that carries the user's access token to an attacker's origin.
const AFTERAPPLY_ROUTES = {
  "local-filter-config": { path: "/api/email-forwarding/local-filter-config", method: "GET", requiresToken: false },
  "extension-signal": { path: "/api/email-forwarding/extension-signal", method: "POST", requiresToken: true },
};

async function afterApplyGetSettings() {
  const result = await chrome.storage.local.get(AFTERAPPLY_SETTINGS_KEY);
  const settings = result[AFTERAPPLY_SETTINGS_KEY] ?? {};
  return {
    apiBaseUrl: settings.apiBaseUrl || AFTERAPPLY_DEFAULT_API_BASE_URL,
    token: settings.token || "",
  };
}

/**
 * Performs one allow-listed API call and returns a plain, JSON-serializable result — a Response
 * object can't cross a chrome.runtime message, and the caller needs the status anyway to tell
 * "the backend said no" (401/429) apart from "the backend never heard us" (network), which is
 * precisely the distinction whose absence made the old silent failure so hard to see.
 *
 * Shape: { ok, status, reason?, body?, etag? }. `ok` is true only for a 2xx — a 304 comes back as
 * ok:false with status 304, which is a meaningful answer for the conditional GET, not a failure.
 */
async function afterApplyCallApi(routeName, { body, ifNoneMatch } = {}) {
  const route = AFTERAPPLY_ROUTES[routeName];
  if (!route) {
    return { ok: false, status: 0, reason: "unknown-route" };
  }

  const settings = await afterApplyGetSettings();
  if (route.requiresToken && !settings.token) {
    // Not an error: the extension is simply not connected to an account yet. Reported rather than
    // thrown so the caller can stay quiet and, importantly, not record the work as done.
    return { ok: false, status: 0, reason: "no-token" };
  }

  const headers = {};
  if (route.requiresToken) {
    headers.Authorization = `Bearer ${settings.token}`;
  }
  if (body !== undefined) {
    headers["Content-Type"] = "application/json";
  }
  if (ifNoneMatch) {
    headers["If-None-Match"] = ifNoneMatch;
  }

  let response;
  try {
    response = await fetch(`${settings.apiBaseUrl}${route.path}`, {
      method: route.method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    // The one line that would have saved this feature: a request the browser refuses to send (no
    // host permission for this API origin, or the API simply unreachable) is indistinguishable
    // from "nothing happened" without it. Route name and origin only — never the token, never the
    // email.
    console.warn(`[e-kariyerim] ${routeName} could not be sent to ${settings.apiBaseUrl} — check that origin is in host_permissions, and that the API is reachable`);
    return { ok: false, status: 0, reason: "network" };
  }

  if (!response.ok) {
    // 304 is an expected answer to the conditional GET, not a problem worth a console line.
    if (response.status !== 304) {
      console.warn(`[e-kariyerim] ${routeName} refused with HTTP ${response.status}`);
    }
    return { ok: false, status: response.status, reason: "http" };
  }

  // 204 (what /extension-signal answers) has no body to read; asking for one throws.
  const text = response.status === 204 ? "" : await response.text();
  let parsed;
  try {
    parsed = text ? JSON.parse(text) : null;
  } catch {
    parsed = null;
  }

  return { ok: true, status: response.status, body: parsed, etag: response.headers.get("ETag") };
}

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message?.type !== AFTERAPPLY_API_MESSAGE) {
    return false; // not ours — let any other listener answer it
  }

  // Only this extension's own Gmail content script has any business asking. sender.id is Chrome's,
  // not the sender's to spoof, and a web page cannot reach this listener at all (no
  // externally_connectable in the manifest) — this is the belt to that braces, and it keeps the
  // check honest if a future manifest change ever widens who can talk to the worker.
  const fromOwnGmailScript = sender.id === chrome.runtime.id && (sender.url || "").startsWith("https://mail.google.com/");
  if (!fromOwnGmailScript) {
    console.warn(`[e-kariyerim] refused a message from an unexpected sender: ${sender.url ?? "(no url)"}`);
    sendResponse({ ok: false, status: 0, reason: "forbidden-sender" });
    return false;
  }

  afterApplyCallApi(message.route, { body: message.body, ifNoneMatch: message.ifNoneMatch })
    .then(sendResponse)
    // The worker must always answer: an unanswered sendMessage leaves the content script's await
    // hanging until the port closes, which surfaces as an unhelpful "message port closed" error.
    .catch(() => sendResponse({ ok: false, status: 0, reason: "worker-error" }));

  return true; // response is async
});
