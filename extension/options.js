import { getSettings, saveSettings } from "./storage.js";
import { setUpThemeToggle } from "./theme.js";

const apiBaseUrlInput = document.getElementById("apiBaseUrl");
const tokenInput = document.getElementById("token");
const statusEl = document.getElementById("status");

async function init() {
  setUpThemeToggle("themeToggle");
  const settings = await getSettings();
  apiBaseUrlInput.value = settings.apiBaseUrl;
  tokenInput.value = settings.token;
}

document.getElementById("save").addEventListener("click", async () => {
  await saveSettings({
    apiBaseUrl: apiBaseUrlInput.value.trim().replace(/\/+$/, ""),
    token: tokenInput.value.trim(),
  });
  statusEl.textContent = "Saved.";
  statusEl.hidden = false;
  setTimeout(() => {
    statusEl.hidden = true;
  }, 2000);
});

init();
