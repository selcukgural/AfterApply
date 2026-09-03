// Bilingual string table + language toggle, shared across every extension page (popup.html,
// options.html). Mirrors theme.js's exact shape: getLanguage/saveLanguage in storage.js, falling
// back to the browser's own language before any explicit choice.
import { getLanguage, saveLanguage } from "./storage.js";

const STRINGS = {
  en: {
    language: "Language",
    popup: {
      pageTitle: "e-kariyerim",
      noJob: "Open a LinkedIn job posting (a /jobs/view/ page, or a job selected in search results) or a kariyer.net job posting (an /is-ilani/ page) to track it here.",
      noToken: "Set up your e-kariyerim access token first.",
      openSettings: "Open Settings",
      autoFillFailed: "Auto-fill failed: ",
      companyLabel: "Company",
      jobTitleLabel: "Job title",
      locationLabel: "Location",
      applyButton: "I Applied",
      requiredFields: "Company and job title are required.",
      alreadyTracked: "Already tracked — opened your existing application.",
      added: "Added to e-kariyerim.",
      networkError: "Could not reach e-kariyerim. Check your Settings (API base URL/token).",
    },
    options: {
      pageTitle: "e-kariyerim — Settings",
      heading: "e-kariyerim Settings",
      tokenHelp: "Generate an access token from e-kariyerim → Settings → Browser Extension, then paste it below.",
      apiBaseUrlLabel: "API base URL",
      tokenLabel: "Access token",
      save: "Save",
      saved: "Saved.",
      gmailScanLabel: "Gmail Scanning (beta)",
      gmailScanHelp: "When you open an email in Gmail, this extension reads it in your browser only and, if it looks job-related, sends just the extracted subject/snippet — never the raw email — as a status suggestion. Off by default. Only threads you actually open are read; nothing else in your inbox is touched.",
      gmailScanToggle: "Scan opened Gmail emails",
    },
  },
  tr: {
    language: "Dil",
    popup: {
      pageTitle: "e-kariyerim",
      noJob: "Buradan takip etmek için bir LinkedIn ilanı (bir /jobs/view/ sayfası ya da arama sonuçlarında seçili bir ilan) veya bir kariyer.net ilanı (bir /is-ilani/ sayfası) açın.",
      noToken: "Önce e-kariyerim erişim anahtarınızı kurun.",
      openSettings: "Ayarları Aç",
      autoFillFailed: "Otomatik doldurma başarısız: ",
      companyLabel: "Şirket",
      jobTitleLabel: "Pozisyon",
      locationLabel: "Konum",
      applyButton: "Başvurdum",
      requiredFields: "Şirket ve pozisyon alanları zorunludur.",
      alreadyTracked: "Zaten takip ediliyor — mevcut başvurunuz açıldı.",
      added: "e-kariyerim'e eklendi.",
      networkError: "e-kariyerim'e ulaşılamadı. Ayarlarınızı (API adresi/anahtar) kontrol edin.",
    },
    options: {
      pageTitle: "e-kariyerim — Ayarlar",
      heading: "e-kariyerim Ayarları",
      tokenHelp: "e-kariyerim → Ayarlar → Tarayıcı Eklentisi üzerinden bir erişim anahtarı oluşturun, ardından aşağıya yapıştırın.",
      apiBaseUrlLabel: "API adresi",
      tokenLabel: "Erişim anahtarı",
      save: "Kaydet",
      saved: "Kaydedildi.",
      gmailScanLabel: "Gmail Taraması (beta)",
      gmailScanHelp: "Gmail'de bir mail açtığınızda, eklenti onu yalnızca tarayıcınızda okur; iş maili gibi görünüyorsa yalnızca çıkarılan konu/özeti — ham maili değil — bir statü önerisi olarak gönderir. Varsayılan olarak kapalıdır. Yalnızca fiilen açtığınız mailler okunur, gelen kutunuzdaki başka hiçbir şeye dokunulmaz.",
      gmailScanToggle: "Açtığım Gmail maillerini tara",
    },
  },
};

export function detectDefaultLanguage() {
  return (navigator.language || "en").toLowerCase().startsWith("tr") ? "tr" : "en";
}

export function t(lang, path) {
  const table = STRINGS[lang] ?? STRINGS.en;
  return path.split(".").reduce((node, key) => node?.[key], table) ?? path;
}

// Applies the stored (or browser-inferred) language immediately, wires up the toggle button, and
// calls onChange(lang) once up front and again on every toggle — the caller re-renders from it.
export async function setUpLanguageToggle(buttonId, onChange) {
  const stored = await getLanguage();
  let current = stored ?? detectDefaultLanguage();

  const button = document.getElementById(buttonId);
  function render() {
    if (button) {
      button.textContent = current === "tr" ? "EN" : "TR";
      button.title = t(current, "language");
    }
  }

  render();
  onChange(current);

  button?.addEventListener("click", async () => {
    current = current === "tr" ? "en" : "tr";
    render();
    await saveLanguage(current);
    onChange(current);
  });
}
