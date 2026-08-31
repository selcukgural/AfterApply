import { getSettings } from "./storage.js";
import { setUpThemeToggle } from "./theme.js";
import { t, setUpLanguageToggle } from "./i18n.js";

const content = document.getElementById("content");
const pageHeading = document.getElementById("pageHeading");
const backLink = document.getElementById("backLink");
backLink.target = "_blank";

const state = {
  lang: "en",
  settings: null,
  address: null,
  gmailConfirmationCode: null,
  gmailConfirmationLink: null,
  addressLoading: true,
  addressError: false,
  addressCopied: false,
  codeCopied: false,
  dismissing: false,
};

function escapeHtml(value) {
  const div = document.createElement("div");
  div.textContent = value ?? "";
  return div.innerHTML;
}

const ICONS = {
  inbox: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round"><path d="M3 12h4l2 3h6l2-3h4"/><path d="M5 6h14l2 6v7a1 1 0 01-1 1H4a1 1 0 01-1-1v-7z"/></svg>`,
  filter: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round"><path d="M4 5h16l-6 8v5l-4 2v-7z"/></svg>`,
  at: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="4"/><path d="M16 12v1.5a2.5 2.5 0 005 0V12a9 9 0 10-4 7.5"/></svg>`,
  eye: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round"><path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7z"/><circle cx="12" cy="12" r="3"/></svg>`,
  check: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round"><path d="M20 6L9 17l-5-5"/></svg>`,
};

function flowDiagram(lang) {
  const steps = [
    [ICONS.inbox, t(lang, "flow.step1")],
    [ICONS.filter, t(lang, "flow.step2")],
    [ICONS.at, t(lang, "flow.step3")],
    [ICONS.eye, t(lang, "flow.step4")],
    [ICONS.check, t(lang, "flow.step5")],
  ];
  return steps
    .map(([icon, label], index) => {
      const arrow = index > 0 ? `<span class="flow-arrow">→</span>` : "";
      return `${arrow}<div class="flow-step"><span class="flow-icon">${icon}</span>${escapeHtml(label)}</div>`;
    })
    .join("");
}

// Stylized, labeled recreation of the relevant Gmail settings panel for each step — not a real
// screenshot, since no live Gmail account is automated for this guide (see extension/README.md).
function gmailMockGear(lang) {
  return `
    <div class="gmail-mock">
      <div class="gmail-mock-bar"><span class="dot"></span><span class="dot"></span><span class="dot"></span>&nbsp;mail.google.com</div>
      <div class="gmail-mock-body">
        <div class="gmail-mock-row"><span>⚙️ ${lang === "tr" ? "Ayarlar" : "Settings"}</span><span class="gmail-mock-btn">${lang === "tr" ? "Tüm ayarları görüntüle" : "See all settings"}</span></div>
      </div>
    </div>`;
}

function gmailMockForwarding(lang, address) {
  return `
    <div class="gmail-mock">
      <div class="gmail-mock-bar"><span class="dot"></span><span class="dot"></span><span class="dot"></span>&nbsp;${lang === "tr" ? "Yönlendirme ve POP/IMAP" : "Forwarding and POP/IMAP"}</div>
      <div class="gmail-mock-body">
        <div class="gmail-mock-row">
          <span class="gmail-mock-input">${escapeHtml(address ?? "your-address@application.ekariyerim.com")}</span>
          <span class="gmail-mock-btn">${lang === "tr" ? "İleri" : "Next"}</span>
        </div>
      </div>
    </div>`;
}

function gmailMockConfirm(lang, code) {
  return `
    <div class="gmail-mock">
      <div class="gmail-mock-bar"><span class="dot"></span><span class="dot"></span><span class="dot"></span>&nbsp;${lang === "tr" ? "Onay bekleniyor" : "Awaiting confirmation"}</div>
      <div class="gmail-mock-body">
        <div class="gmail-mock-row">
          <span class="gmail-mock-input">${code ? escapeHtml(code) : "••••••"}</span>
          <span class="gmail-mock-btn">${lang === "tr" ? "Onayla" : "Confirm"}</span>
        </div>
      </div>
    </div>`;
}

function gmailMockFilter(lang, address) {
  return `
    <div class="gmail-mock">
      <div class="gmail-mock-bar"><span class="dot"></span><span class="dot"></span><span class="dot"></span>&nbsp;${lang === "tr" ? "Yeni filtre oluştur" : "Create a new filter"}</div>
      <div class="gmail-mock-body">
        <div class="gmail-mock-row"><span>☑ ${lang === "tr" ? "Şuraya yönlendir:" : "Forward it to:"}</span><span class="gmail-mock-input">${escapeHtml(address ?? "your-address@application.ekariyerim.com")}</span></div>
        <div class="gmail-mock-row"><span></span><span class="gmail-mock-btn">${lang === "tr" ? "Filtre Oluştur" : "Create filter"}</span></div>
      </div>
    </div>`;
}

function addressCardHtml(lang) {
  if (!state.settings?.token) {
    return `
      <div class="address-card">
        <p class="muted">${escapeHtml(t(lang, "address.needsToken"))}</p>
        <button id="openSettingsBtn" class="secondary">${escapeHtml(t(lang, "address.openSettings"))}</button>
      </div>`;
  }

  if (state.addressLoading) {
    return `<div class="address-card"><p class="muted">${escapeHtml(t(lang, "address.loading"))}</p></div>`;
  }

  if (state.addressError) {
    return `<div class="address-card"><p class="status error">${escapeHtml(t(lang, "address.error"))}</p></div>`;
  }

  const confirmation = state.gmailConfirmationCode || state.gmailConfirmationLink
    ? `
      <div class="confirmation-banner">
        <p class="label">${escapeHtml(t(lang, "steps.step3.codeLabel"))}</p>
        <div class="code-chip-row">
          <span class="code-chip">${escapeHtml(state.gmailConfirmationCode ?? "—")}</span>
          <button id="copyCodeBtn" class="secondary">${state.codeCopied ? escapeHtml(t(lang, "address.copied")) : escapeHtml(t(lang, "address.copy"))}</button>
        </div>
        <div class="actions">
          ${state.gmailConfirmationLink ? `<a href="${escapeHtml(state.gmailConfirmationLink)}" target="_blank" rel="noopener noreferrer">${escapeHtml(t(lang, "steps.step3.openLink"))}</a>` : ""}
          <button id="dismissConfirmationBtn" type="button" class="link" ${state.dismissing ? "disabled" : ""}>${escapeHtml(t(lang, "steps.step3.dismiss"))}</button>
        </div>
      </div>`
    : "";

  return `
    <div class="address-card">
      <h2>${escapeHtml(t(lang, "address.title"))}</h2>
      <div class="code-chip-row">
        <span class="code-chip">${escapeHtml(state.address ?? "")}</span>
        <button id="copyAddressBtn" class="secondary">${state.addressCopied ? escapeHtml(t(lang, "address.copied")) : escapeHtml(t(lang, "address.copy"))}</button>
      </div>
      ${confirmation}
    </div>`;
}

function render() {
  const lang = state.lang;
  document.title = t(lang, "pageTitle");
  pageHeading.textContent = "e-kariyerim";
  backLink.textContent = t(lang, "backToSettings");

  const address = state.address;

  content.innerHTML = `
    <section class="guide-section hero">
      <h1>${escapeHtml(t(lang, "hero.title"))}</h1>
      <p>${escapeHtml(t(lang, "hero.body"))}</p>
      <div class="trust-box">
        <p>${escapeHtml(t(lang, "hero.trustTitle"))}</p>
        <ul>
          <li>${escapeHtml(t(lang, "hero.trust1"))}</li>
          <li>${escapeHtml(t(lang, "hero.trust2"))}</li>
          <li>${escapeHtml(t(lang, "hero.trust3"))}</li>
          <li>${escapeHtml(t(lang, "hero.trust4"))}</li>
        </ul>
      </div>
    </section>

    <section class="guide-section">
      <h2 style="font-size:14px;font-weight:700;margin:0 0 14px;">${escapeHtml(t(lang, "flow.title"))}</h2>
      <div class="flow-diagram">${flowDiagram(lang)}</div>
    </section>

    <section class="guide-section">
      ${addressCardHtml(lang)}
    </section>

    <section class="guide-section">
      <h2 style="font-size:14px;font-weight:700;margin:0 0 4px;">${escapeHtml(t(lang, "steps.title"))}</h2>

      <div class="step-card">
        <span class="step-number">1</span>
        <div class="step-body">
          <h3>${escapeHtml(t(lang, "steps.step1.title"))}</h3>
          <p>${escapeHtml(t(lang, "steps.step1.body"))}</p>
          ${gmailMockGear(lang)}
        </div>
      </div>

      <div class="step-card">
        <span class="step-number">2</span>
        <div class="step-body">
          <h3>${escapeHtml(t(lang, "steps.step2.title"))}</h3>
          <p>${escapeHtml(t(lang, "steps.step2.body"))}</p>
          ${gmailMockForwarding(lang, address)}
        </div>
      </div>

      <div class="step-card">
        <span class="step-number">3</span>
        <div class="step-body">
          <h3>${escapeHtml(t(lang, "steps.step3.title"))}</h3>
          <p>${escapeHtml(t(lang, "steps.step3.body"))}</p>
          ${gmailMockConfirm(lang, state.gmailConfirmationCode)}
          ${!state.gmailConfirmationCode ? `<p class="muted" style="margin-top:8px;">${escapeHtml(t(lang, "steps.step3.waitingForCode"))} <button id="refreshAddressBtn" class="secondary" style="width:auto;display:inline-block;margin:6px 0 0;">${escapeHtml(t(lang, "steps.step3.refresh"))}</button></p>` : ""}
        </div>
      </div>

      <div class="step-card">
        <span class="step-number">4</span>
        <div class="step-body">
          <h3>${escapeHtml(t(lang, "steps.step4.title"))}</h3>
          <p>${escapeHtml(t(lang, "steps.step4.body"))}</p>
          ${gmailMockFilter(lang, address)}
        </div>
      </div>

      <div class="step-card">
        <span class="step-number">5</span>
        <div class="step-body">
          <h3>${escapeHtml(t(lang, "steps.step5.title"))}</h3>
          <p>${escapeHtml(t(lang, "steps.step5.body"))}</p>
          <a href="https://ekariyerim.com/settings/email-suggestions" target="_blank" rel="noopener noreferrer">${escapeHtml(t(lang, "steps.step5.viewSuggestions"))}</a>
        </div>
      </div>
    </section>

    <section class="guide-section">
      <h2 style="font-size:14px;font-weight:700;margin:0 0 4px;">${escapeHtml(t(lang, "faq.title"))}</h2>
      <dl class="faq">
        <dt>${escapeHtml(t(lang, "faq.q1"))}</dt>
        <dd>${escapeHtml(t(lang, "faq.a1"))}</dd>
        <dt>${escapeHtml(t(lang, "faq.q2"))}</dt>
        <dd>${escapeHtml(t(lang, "faq.a2"))}</dd>
        <dt>${escapeHtml(t(lang, "faq.q3"))}</dt>
        <dd>${escapeHtml(t(lang, "faq.a3"))}</dd>
      </dl>
    </section>
  `;

  attachListeners();
}

function attachListeners() {
  document.getElementById("openSettingsBtn")?.addEventListener("click", () => chrome.runtime.openOptionsPage());

  document.getElementById("copyAddressBtn")?.addEventListener("click", async () => {
    if (!state.address) return;
    await navigator.clipboard.writeText(state.address);
    state.addressCopied = true;
    render();
  });

  document.getElementById("copyCodeBtn")?.addEventListener("click", async () => {
    if (!state.gmailConfirmationCode) return;
    await navigator.clipboard.writeText(state.gmailConfirmationCode);
    state.codeCopied = true;
    render();
  });

  document.getElementById("refreshAddressBtn")?.addEventListener("click", () => fetchAddress());

  document.getElementById("dismissConfirmationBtn")?.addEventListener("click", async () => {
    state.dismissing = true;
    render();
    try {
      await fetch(`${state.settings.apiBaseUrl}/api/email-forwarding/gmail-confirmation/dismiss`, {
        method: "POST",
        headers: { Authorization: `Bearer ${state.settings.token}` },
      });
    } catch {
      // Best-effort — worst case the banner reappears on next refresh, not a lost user action.
    }
    state.gmailConfirmationCode = null;
    state.gmailConfirmationLink = null;
    state.dismissing = false;
    render();
  });
}

async function fetchAddress() {
  if (!state.settings?.token) {
    state.addressLoading = false;
    render();
    return;
  }

  state.addressLoading = true;
  state.addressError = false;
  render();

  try {
    const response = await fetch(`${state.settings.apiBaseUrl}/api/email-forwarding/address`, {
      headers: { Authorization: `Bearer ${state.settings.token}` },
    });
    if (!response.ok) {
      throw new Error(`Request failed (${response.status})`);
    }
    const data = await response.json();
    state.address = data.address;
    state.gmailConfirmationCode = data.gmailConfirmationCode;
    state.gmailConfirmationLink = data.gmailConfirmationLink;
  } catch {
    state.addressError = true;
  } finally {
    state.addressLoading = false;
    render();
  }
}

async function main() {
  setUpThemeToggle("themeToggle");
  state.settings = await getSettings();

  await setUpLanguageToggle("langToggle", (lang) => {
    state.lang = lang;
    render();
  });

  await fetchAddress();
}

main();
