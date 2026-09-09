import {
  getSettings,
  saveSettings,
  getGmailScanEnabled,
  setGmailScanEnabled,
  daysUntilExpiry,
  EXPIRY_WARNING_DAYS,
} from "./storage.js";
import { setUpThemeToggle } from "./theme.js";
import { t, setUpLanguageToggle } from "./i18n.js";
import { renderVersion } from "./version.js";
import { startPairing, pollPairing, isTerminalStatus, formatPairingCode } from "./pairing.js";

const apiBaseUrlInput = document.getElementById("apiBaseUrl");
const tokenInput = document.getElementById("token");
const statusEl = document.getElementById("status");
const gmailScanEnabledInput = document.getElementById("gmailScanEnabled");
const connectButton = document.getElementById("connect");
const connectionStateEl = document.getElementById("connectionState");
const pairingCodeEl = document.getElementById("pairingCode");
const pairingStatusEl = document.getElementById("pairingStatus");
const openPairingPageButton = document.getElementById("openPairingPage");

let currentLang = "en";

// The pairing in flight, if any: { code, deviceSecret, verificationUrl, expiresAt,
// pollIntervalSeconds }. Null between attempts.
let pairing = null;
let pollHandle = null;

// What the pairing area is saying right now, kept as a key (plus its substitutions) rather than as
// rendered text, so a language toggle mid-flow re-renders it in the new language — the same reason
// popup.js keeps statusKey.
let pairingMessage = null;

/** Text of every static label/heading/button in the current language. Never touches
 * apiBaseUrlInput/tokenInput's .value, so a language switch can't disturb an in-progress edit. */
function applyLanguage(lang) {
  currentLang = lang;
  document.title = t(lang, "options.pageTitle");
  document.getElementById("pageHeading").textContent = t(lang, "options.heading");
  document.getElementById("tokenHelp").textContent = t(lang, "options.tokenHelp");
  document.getElementById("manualTitle").textContent = t(lang, "options.manualTitle");
  document.getElementById("apiBaseUrlLabel").textContent = t(lang, "options.apiBaseUrlLabel");
  document.getElementById("tokenLabel").textContent = t(lang, "options.tokenLabel");
  document.getElementById("save").textContent = t(lang, "options.save");
  document.getElementById("gmailScanLabel").textContent = t(lang, "options.gmailScanLabel");
  document.getElementById("gmailScanHelp").textContent = t(lang, "options.gmailScanHelp");
  document.getElementById("gmailScanToggle").textContent = t(lang, "options.gmailScanToggle");
  connectButton.textContent = t(lang, pairing ? "options.connecting" : "options.connect");
  openPairingPageButton.textContent = t(lang, "options.openPage");
  renderVersion(lang);

  if (statusEl.dataset.i18nKey) {
    statusEl.textContent = t(lang, statusEl.dataset.i18nKey);
  }

  renderConnectionState();
  renderPairingMessage();
}

/** Sets a message on an element from a key, substituting {name} placeholders. */
function setMessage(element, message, tone) {
  if (!message) {
    element.hidden = true;
    return;
  }

  let text = t(currentLang, message.key);
  for (const [name, value] of Object.entries(message.values ?? {})) {
    text = text.replace(`{${name}}`, value);
  }

  element.textContent = text;
  element.className = `status ${tone}`;
  element.hidden = false;
}

/**
 * The standing line above the Connect button: connected, connected-but-expiring, expired, or not
 * connected at all. The expiring case is the whole reason the extension now stores an expiry —
 * before this, a connection simply stopped working one day, ninety days after it was made, with
 * nothing anywhere having said so.
 */
let connectionMessage = null;

function renderConnectionState() {
  setMessage(connectionStateEl, connectionMessage?.message, connectionMessage?.tone ?? "success");
}

function renderPairingMessage() {
  setMessage(pairingStatusEl, pairingMessage?.message, pairingMessage?.tone ?? "warning");
}

function describeConnection(settings) {
  if (!settings.token) {
    connectionMessage = { message: { key: "options.notConnected" }, tone: "warning" };
    return;
  }

  const days = daysUntilExpiry(settings.tokenExpiresAt);

  if (days === null) {
    // A key pasted in by hand, or one from before pairing existed: it works, we just don't know
    // for how long. Claiming a date here would be inventing one.
    connectionMessage = { message: { key: "options.connected" }, tone: "success" };
    return;
  }

  if (days < 0) {
    connectionMessage = { message: { key: "options.expired" }, tone: "error" };
    return;
  }

  if (days <= EXPIRY_WARNING_DAYS) {
    connectionMessage = { message: { key: "options.expiryWarning", values: { days } }, tone: "warning" };
    return;
  }

  connectionMessage = {
    message: {
      key: "options.connectedUntil",
      values: { date: new Date(settings.tokenExpiresAt).toLocaleDateString() },
    },
    tone: "success",
  };
}

function stopPolling() {
  if (pollHandle) {
    clearTimeout(pollHandle);
    pollHandle = null;
  }
}

function endPairing(message, tone) {
  stopPolling();
  pairing = null;
  pairingCodeEl.hidden = true;
  openPairingPageButton.hidden = true;
  connectButton.disabled = false;
  connectButton.textContent = t(currentLang, "options.connect");
  pairingMessage = message ? { message, tone } : null;
  renderPairingMessage();
}

async function applyPairedToken(poll) {
  const settings = await getSettings();
  await saveSettings({
    apiBaseUrl: apiBaseUrlInput.value.trim().replace(/\/+$/, "") || settings.apiBaseUrl,
    token: poll.token,
    tokenExpiresAt: poll.tokenExpiresAt ?? "",
  });

  tokenInput.value = poll.token;
  gmailScanEnabledInput.disabled = false;
  describeConnection(await getSettings());
  renderConnectionState();
}

async function pollOnce() {
  if (!pairing) {
    return;
  }

  // The server expires the request on its own; this is the client-side counterpart, so a tab left
  // open overnight stops asking rather than polling a dead code until it is closed.
  if (new Date(pairing.expiresAt).getTime() <= Date.now()) {
    endPairing({ key: "options.pairExpired" }, "warning");
    return;
  }

  let poll;
  try {
    poll = await pollPairing(pairing.apiBaseUrl, pairing.deviceSecret);
  } catch {
    // A network blip mid-pairing is not a failed pairing: keep asking until the code expires.
    schedulePoll();
    return;
  }

  if (poll.status === "Completed" && poll.token) {
    await applyPairedToken(poll);
    endPairing(null);
    return;
  }

  if (poll.status === "TokenLimitReached") {
    // Recoverable without starting over: revoking a key on the settings page is enough, and the
    // next poll picks the pairing up where it was. So the message is shown and polling continues.
    pairingMessage = { message: { key: "options.pairLimitReached" }, tone: "warning" };
    renderPairingMessage();
    schedulePoll();
    return;
  }

  if (isTerminalStatus(poll.status)) {
    const keys = {
      Denied: "options.pairDenied",
      Expired: "options.pairExpired",
      // Completed with no token: this pairing already handed its token to someone (a second
      // options tab). Nothing to do here, and nothing was lost.
      Completed: "options.connected",
    };
    endPairing({ key: keys[poll.status] }, poll.status === "Completed" ? "success" : "warning");
    return;
  }

  schedulePoll();
}

function schedulePoll() {
  stopPolling();
  pollHandle = setTimeout(() => void pollOnce(), (pairing?.pollIntervalSeconds ?? 3) * 1000);
}

async function connect() {
  stopPolling();
  connectButton.disabled = true;
  connectButton.textContent = t(currentLang, "options.connecting");
  pairingMessage = null;
  renderPairingMessage();

  const apiBaseUrl = apiBaseUrlInput.value.trim().replace(/\/+$/, "") || (await getSettings()).apiBaseUrl;

  let started;
  try {
    started = await startPairing(apiBaseUrl, currentLang);
  } catch {
    endPairing({ key: "options.pairFailed" }, "error");
    return;
  }

  pairing = { ...started, apiBaseUrl };

  pairingCodeEl.textContent = formatPairingCode(started.code);
  pairingCodeEl.hidden = false;
  openPairingPageButton.hidden = false;
  pairingMessage = { message: { key: "options.codePrompt" }, tone: "warning" };
  renderPairingMessage();

  // A normal tab, from a page that stays alive — this is why pairing runs here and not in the
  // popup, which Chrome tears down the moment this tab takes focus.
  chrome.tabs.create({ url: started.verificationUrl });

  schedulePoll();
}

async function init() {
  setUpThemeToggle("themeToggle");
  await setUpLanguageToggle("langToggle", applyLanguage);

  const settings = await getSettings();
  apiBaseUrlInput.value = settings.apiBaseUrl;
  tokenInput.value = settings.token;

  describeConnection(settings);
  renderConnectionState();

  // Scanning without a token has nowhere to send a signal. gmail-scan.js independently re-checks
  // the token itself before ever submitting, this is just UI-level guidance.
  gmailScanEnabledInput.checked = await getGmailScanEnabled();
  gmailScanEnabledInput.disabled = !settings.token;

  // The popup opens this page as options.html?pair=1 when someone presses Connect there, so the
  // click they already made is the only one they have to make.
  if (new URLSearchParams(location.search).get("pair") === "1") {
    await connect();
  }
}

connectButton.addEventListener("click", () => void connect());

openPairingPageButton.addEventListener("click", () => {
  if (pairing) {
    chrome.tabs.create({ url: pairing.verificationUrl });
  }
});

document.getElementById("save").addEventListener("click", async () => {
  const token = tokenInput.value.trim();
  const previous = await getSettings();

  await saveSettings({
    apiBaseUrl: apiBaseUrlInput.value.trim().replace(/\/+$/, ""),
    token,
    // A key typed in by hand carries no expiry with it, so any expiry remembered from a previous
    // pairing is now a claim about a different key. Dropped rather than kept: an unknown expiry
    // shows no date, a wrong one would show a false date.
    tokenExpiresAt: token === previous.token ? previous.tokenExpiresAt : "",
  });

  gmailScanEnabledInput.disabled = !token;
  describeConnection(await getSettings());
  renderConnectionState();

  statusEl.dataset.i18nKey = "options.saved";
  statusEl.textContent = t(currentLang, "options.saved");
  statusEl.className = "status success";
  statusEl.hidden = false;
  setTimeout(() => {
    statusEl.hidden = true;
    delete statusEl.dataset.i18nKey;
  }, 2000);
});

gmailScanEnabledInput.addEventListener("change", async () => {
  await setGmailScanEnabled(gmailScanEnabledInput.checked);
});

init();
