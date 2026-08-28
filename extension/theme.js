// Shared light/dark toggle for popup.js and options.js. Mirrors the web app's own theme model
// (web/src/lib/theme/theme.ts) but with no account sync — an extension install has no session to
// sync to, so the choice is just the per-install storage.js preference (or the OS setting, until
// the user picks one explicitly).
import { getTheme, saveTheme } from "./storage.js";

const SUN_ICON = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41"/></svg>`;
const MOON_ICON = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12.79A9 9 0 1111.21 3 7 7 0 0021 12.79z"/></svg>`;

function prefersDark() {
  return window.matchMedia?.("(prefers-color-scheme: dark)").matches ?? false;
}

function applyTheme(theme) {
  document.documentElement.dataset.theme = theme;
}

// Called once per page load. Applies the stored (or OS-inferred) theme immediately and wires up
// the toggle button so clicking it flips + persists the explicit choice.
export async function setUpThemeToggle(buttonId) {
  const stored = await getTheme();
  let current = stored ?? (prefersDark() ? "dark" : "light");
  applyTheme(current);

  const button = document.getElementById(buttonId);
  if (!button) {
    return;
  }

  function render() {
    button.innerHTML = current === "dark" ? SUN_ICON : MOON_ICON;
    button.setAttribute("aria-label", current === "dark" ? "Switch to light theme" : "Switch to dark theme");
    button.title = button.getAttribute("aria-label");
  }

  render();
  button.addEventListener("click", async () => {
    current = current === "dark" ? "light" : "dark";
    applyTheme(current);
    render();
    await saveTheme(current);
  });
}
