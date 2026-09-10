// Bilingual string table + language toggle, shared across every extension page (popup.html,
// options.html). Mirrors theme.js's exact shape: getLanguage/saveLanguage in storage.js, falling
// back to the browser's own language before any explicit choice.
import { getLanguage, saveLanguage } from "./storage.js";

const STRINGS = {
  en: {
    language: "Language",
    versionLabel: "Version {version}",
    privacyLink: "Privacy policy",
    popup: {
      pageTitle: "e-kariyerim",
      noJob: "Open a LinkedIn job posting (a /jobs/view/ page, or a job selected in search results) or a kariyer.net job posting (an /is-ilani/ page) to track it here.",
      noToken: "This extension is not connected to an e-kariyerim account yet.",
      tokenExpired: "The connection to your e-kariyerim account has expired. Connect again — it takes one click.",
      connect: "Connect",
      openSettings: "Open Settings",
      expiryWarning: "The connection expires in {days} days. Reconnect from Settings whenever you like.",
      autoFillFailed: "Auto-fill failed: ",
      companyLabel: "Company",
      jobTitleLabel: "Job title",
      locationLabel: "Location",
      hrNameLabel: "HR contact (optional)",
      hrEmailLabel: "HR email (optional)",
      hrLinkedInLabel: "HR LinkedIn (optional)",
      applyButton: "I Applied",
      requiredFields: "Company and job title are required.",
      alreadyTracked: "Already tracked — opened your existing application.",
      added: "Added to e-kariyerim.",
      networkError: "Could not reach e-kariyerim. Check your Settings (API base URL/token).",
      unauthorized: "e-kariyerim did not accept the connection — it has probably expired. Open Settings and connect again.",
    },
    options: {
      pageTitle: "e-kariyerim — Settings",
      heading: "e-kariyerim Settings",
      tokenHelp: "Press Connect and confirm on the page that opens. No copying, no pasting.",
      connect: "Connect",
      connecting: "Starting...",
      cancel: "Cancel",
      reconnect: "Connect again",
      codePrompt: "Confirm this code on the page that just opened:",
      waiting: "Waiting for confirmation in the other tab...",
      openPage: "Open the confirmation page again",
      connected: "Connected. Your applications will land in this account.",
      connectedUntil: "Connected. The connection is valid until {date}.",
      expiryWarning: "The connection expires in {days} days. Press Connect to renew it.",
      expired: "The connection has expired. Press Connect to set it up again.",
      notConnected: "Not connected yet.",
      pairFailed: "Could not start the connection. Check the API base URL and try again.",
      pairDenied: "The request was refused on the confirmation page. Nothing was connected.",
      pairExpired: "The code expired before it was confirmed. Press Connect to try again.",
      pairLimitReached: "That account already holds the maximum number of active keys. Revoke one under Settings → Browser Extension; this page will connect on its own.",
      manualTitle: "Enter a key by hand (advanced)",
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
    versionLabel: "Sürüm {version}",
    privacyLink: "Gizlilik politikası",
    popup: {
      pageTitle: "e-kariyerim",
      noJob: "Buradan takip etmek için bir LinkedIn ilanı (bir /jobs/view/ sayfası ya da arama sonuçlarında seçili bir ilan) veya bir kariyer.net ilanı (bir /is-ilani/ sayfası) açın.",
      noToken: "Bu eklenti henüz bir e-kariyerim hesabına bağlı değil.",
      tokenExpired: "e-kariyerim hesabınla bağlantının süresi doldu. Tek tıkla yeniden bağlanabilirsin.",
      connect: "Bağlan",
      openSettings: "Ayarları Aç",
      expiryWarning: "Bağlantının süresi {days} gün sonra doluyor. Ayarlar'dan dilediğin zaman yenileyebilirsin.",
      autoFillFailed: "Otomatik doldurma başarısız: ",
      companyLabel: "Şirket",
      jobTitleLabel: "Pozisyon",
      locationLabel: "Konum",
      hrNameLabel: "İK kontağı (isteğe bağlı)",
      hrEmailLabel: "İK e-posta (isteğe bağlı)",
      hrLinkedInLabel: "İK LinkedIn (isteğe bağlı)",
      applyButton: "Başvurdum",
      requiredFields: "Şirket ve pozisyon alanları zorunludur.",
      alreadyTracked: "Zaten takip ediliyor — mevcut başvurunuz açıldı.",
      added: "e-kariyerim'e eklendi.",
      networkError: "e-kariyerim'e ulaşılamadı. Ayarlarınızı (API adresi/anahtar) kontrol edin.",
      unauthorized: "e-kariyerim bağlantıyı kabul etmedi — büyük olasılıkla süresi doldu. Ayarlar'ı açıp yeniden bağlan.",
    },
    options: {
      pageTitle: "e-kariyerim — Ayarlar",
      heading: "e-kariyerim Ayarları",
      tokenHelp: "Bağlan'a bas ve açılan sayfada onayla. Kopyalama yok, yapıştırma yok.",
      connect: "Bağlan",
      connecting: "Başlatılıyor...",
      cancel: "Vazgeç",
      reconnect: "Yeniden bağlan",
      codePrompt: "Az önce açılan sayfada bu kodu onayla:",
      waiting: "Diğer sekmede onay bekleniyor...",
      openPage: "Onay sayfasını yeniden aç",
      connected: "Bağlandı. Başvurularınız bu hesaba düşecek.",
      connectedUntil: "Bağlandı. Bağlantı {date} tarihine kadar geçerli.",
      expiryWarning: "Bağlantının süresi {days} gün sonra doluyor. Yenilemek için Bağlan'a bas.",
      expired: "Bağlantının süresi doldu. Yeniden kurmak için Bağlan'a bas.",
      notConnected: "Henüz bağlı değil.",
      pairFailed: "Bağlantı başlatılamadı. API adresini kontrol edip tekrar dene.",
      pairDenied: "İstek onay sayfasında reddedildi. Hiçbir şey bağlanmadı.",
      pairExpired: "Kod onaylanmadan süresi doldu. Tekrar denemek için Bağlan'a bas.",
      pairLimitReached: "O hesapta izin verilen en fazla sayıda aktif anahtar var. Ayarlar → Tarayıcı Eklentisi'nden birini iptal et; bu sayfa kendiliğinden bağlanacak.",
      manualTitle: "Anahtarı elle gir (gelişmiş)",
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
