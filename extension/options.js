import { getSettings, saveSettings, getGmailScanEnabled, setGmailScanEnabled } from "./storage.js";
import { setUpThemeToggle } from "./theme.js";
import { t, setUpLanguageToggle } from "./i18n.js";

const apiBaseUrlInput = document.getElementById("apiBaseUrl");
const tokenInput = document.getElementById("token");
const statusEl = document.getElementById("status");
const openEmailForwardingButton = document.getElementById("openEmailForwarding");
const gmailScanEnabledInput = document.getElementById("gmailScanEnabled");

let currentLang = "en";

// Sets the text of every static label/heading/button from the current language — never touches
// apiBaseUrlInput/tokenInput's .value, so a language switch can't disturb an in-progress edit.
function applyLanguage(lang) {
  currentLang = lang;
  document.title = t(lang, "options.pageTitle");
  document.getElementById("pageHeading").textContent = t(lang, "options.heading");
  document.getElementById("tokenHelp").textContent = t(lang, "options.tokenHelp");
  document.getElementById("apiBaseUrlLabel").textContent = t(lang, "options.apiBaseUrlLabel");
  document.getElementById("tokenLabel").textContent = t(lang, "options.tokenLabel");
  document.getElementById("save").textContent = t(lang, "options.save");
  document.getElementById("forwardingLabel").textContent = t(lang, "options.forwardingLabel");
  document.getElementById("forwardingHelp").textContent = t(lang, "options.forwardingHelp");
  openEmailForwardingButton.textContent = t(lang, "options.setUpForwarding");
  document.getElementById("gmailScanLabel").textContent = t(lang, "options.gmailScanLabel");
  document.getElementById("gmailScanHelp").textContent = t(lang, "options.gmailScanHelp");
  document.getElementById("gmailScanToggle").textContent = t(lang, "options.gmailScanToggle");

  if (statusEl.dataset.i18nKey) {
    statusEl.textContent = t(lang, statusEl.dataset.i18nKey);
  }
}

async function init() {
  setUpThemeToggle("themeToggle");
  await setUpLanguageToggle("langToggle", applyLanguage);

  const settings = await getSettings();
  apiBaseUrlInput.value = settings.apiBaseUrl;
  tokenInput.value = settings.token;

  // The guide reads the token itself once opened — gating the button just avoids sending someone
  // straight to a "set up your token first" dead end.
  openEmailForwardingButton.disabled = !settings.token;

  // Same gating reason as the forwarding button — scanning without a token has nowhere to send a
  // signal. gmail-scan.js independently re-checks the token itself before ever submitting, this is
  // just UI-level guidance.
  gmailScanEnabledInput.checked = await getGmailScanEnabled();
  gmailScanEnabledInput.disabled = !settings.token;
}

document.getElementById("save").addEventListener("click", async () => {
  const token = tokenInput.value.trim();
  await saveSettings({
    apiBaseUrl: apiBaseUrlInput.value.trim().replace(/\/+$/, ""),
    token,
  });
  openEmailForwardingButton.disabled = !token;
  gmailScanEnabledInput.disabled = !token;
  statusEl.dataset.i18nKey = "options.saved";
  statusEl.textContent = t(currentLang, "options.saved");
  statusEl.hidden = false;
  setTimeout(() => {
    statusEl.hidden = true;
    delete statusEl.dataset.i18nKey;
  }, 2000);
});

openEmailForwardingButton.addEventListener("click", () => {
  window.open(chrome.runtime.getURL("email-forwarding.html"), "_blank");
});

gmailScanEnabledInput.addEventListener("change", async () => {
  await setGmailScanEnabled(gmailScanEnabledInput.checked);
});

init();
