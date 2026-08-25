// Shared chrome.storage.local access for popup.js and options.js. Per-viewer/per-install
// settings only (API base URL + personal access token) — never synced, never sent anywhere
// except as the Authorization header on requests this extension itself makes to that API.

const STORAGE_KEY = "afterapply_settings";
const DEFAULT_API_BASE_URL = "http://localhost:5151";

export async function getSettings() {
  const result = await chrome.storage.local.get(STORAGE_KEY);
  const settings = result[STORAGE_KEY] ?? {};
  return {
    apiBaseUrl: settings.apiBaseUrl || DEFAULT_API_BASE_URL,
    token: settings.token || "",
  };
}

export async function saveSettings(settings) {
  await chrome.storage.local.set({ [STORAGE_KEY]: settings });
}
