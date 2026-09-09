// The extension's half of the pairing handshake (see IExtensionPairingService on the API side).
//
// Why this exists: the extension used to need an access token typed in by hand, which meant
// registering, finding Settings, generating a key, copying it, opening this page and pasting it —
// six steps in front of the one part of the product that costs a single click to use. Here the
// extension asks for a code, the person confirms that code once in a browser they are already
// signed into, and the token arrives on its own.
//
// Two halves, deliberately: the *code* is short and shown to a human, the *device secret* is
// 256 bits and never leaves this extension. Knowing the code lets you confirm a pairing; only the
// secret collects the token it produces.
//
// Note the polling lives on the options page rather than the popup. A popup is torn down the
// moment focus moves to another tab — which is exactly what opening the confirmation page does —
// so a poll loop started there would die two seconds in.

/** Terminal states: nothing about them changes on a later poll. */
const TERMINAL_STATUSES = new Set(["Completed", "Denied", "Expired"]);

export function isTerminalStatus(status) {
  return TERMINAL_STATUSES.has(status);
}

async function postJson(url, body) {
  const response = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });

  if (!response.ok) {
    const error = new Error(`Request failed (${response.status})`);
    error.status = response.status;
    throw error;
  }

  return await response.json();
}

/**
 * Starts a pairing. Returns { code, deviceSecret, expiresAt, verificationUrl, pollIntervalSeconds }.
 * Anonymous — there is no credential yet, which is the whole point.
 */
export function startPairing(apiBaseUrl, locale) {
  return postJson(`${apiBaseUrl}/api/extension-pairing/requests`, { locale });
}

/**
 * Asks whether the pairing has been confirmed. Returns { status, token, tokenExpiresAt } — the
 * token is present exactly once, on the first poll after the confirmation, and never again.
 *
 * A 404 means the server has never heard of this secret (the request was swept away, or the
 * extension is pointed at a different API); it surfaces as `{ status: "Expired" }` so the caller
 * has one "start over" path instead of two.
 */
export async function pollPairing(apiBaseUrl, deviceSecret) {
  try {
    return await postJson(`${apiBaseUrl}/api/extension-pairing/poll`, { deviceSecret });
  } catch (error) {
    if (error.status === 404) {
      return { status: "Expired" };
    }
    throw error;
  }
}

/** "K7M3QXAB" → "K7M3-QXAB". Split for reading and for comparing against the confirmation page. */
export function formatPairingCode(code) {
  return typeof code === "string" && code.length === 8 ? `${code.slice(0, 4)}-${code.slice(4)}` : code;
}
