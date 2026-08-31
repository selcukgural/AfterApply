import { getSettings, saveSettings } from "./storage.js";
import { setUpThemeToggle } from "./theme.js";

const apiBaseUrlInput = document.getElementById("apiBaseUrl");
const tokenInput = document.getElementById("token");
const statusEl = document.getElementById("status");
const openEmailForwardingButton = document.getElementById("openEmailForwarding");

async function init() {
  setUpThemeToggle("themeToggle");
  const settings = await getSettings();
  apiBaseUrlInput.value = settings.apiBaseUrl;
  tokenInput.value = settings.token;

  // The guide reads the token itself once opened — gating the button just avoids sending someone
  // straight to a "set up your token first" dead end.
  openEmailForwardingButton.disabled = !settings.token;
}

document.getElementById("save").addEventListener("click", async () => {
  const token = tokenInput.value.trim();
  await saveSettings({
    apiBaseUrl: apiBaseUrlInput.value.trim().replace(/\/+$/, ""),
    token,
  });
  openEmailForwardingButton.disabled = !token;
  statusEl.textContent = "Saved.";
  statusEl.hidden = false;
  setTimeout(() => {
    statusEl.hidden = true;
  }, 2000);
});

openEmailForwardingButton.addEventListener("click", () => {
  window.open(chrome.runtime.getURL("email-forwarding.html"), "_blank");
});

init();
