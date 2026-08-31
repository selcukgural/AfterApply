// Minimal bilingual string table + language toggle for email-forwarding.html only. The rest of
// the extension (popup.js/options.js) stays English-only — a separate, pre-existing choice, not
// touched here. Mirrors theme.js's exact shape: getLanguage/saveLanguage in storage.js, falling
// back to the browser's own language before any explicit choice.
import { getLanguage, saveLanguage } from "./storage.js";

const STRINGS = {
  en: {
    pageTitle: "e-kariyerim — Email Forwarding Setup",
    backToSettings: "← Back to Settings",
    hero: {
      title: "Turn your inbox into automatic status updates",
      body: "Forward job-application emails (interview invites, rejections, status updates) to your personal e-kariyerim address, and they'll show up as suggestions you approve — no more manually updating every application.",
      trustTitle: "Before you start, the short version:",
      trust1: "No sign-in to your email account. Ever. We never ask for your Gmail password or an OAuth grant.",
      trust2: "You turn on forwarding once, in your own Gmail settings — after that every incoming email is relayed to us automatically. No per-sender filter to create or maintain.",
      trust3: "We only turn a forwarded email into something you see when it's from a known job site/ATS or a company you've already added to e-kariyerim — **everything else is discarded immediately: never stored, never used for any other purpose, never seen by anyone.**",
      trust4: "Undo it anytime by turning off forwarding in Gmail's own \"Forwarding and POP/IMAP\" settings. Nothing on our side needs to change.",
    },
    flow: {
      title: "How it works",
      step1: "Your Gmail inbox",
      step2: "Forward everything (one-time)",
      step3: "Your personal e-kariyerim address",
      step4: "We read only the subject + a snippet",
      step5: "You approve the suggestion",
    },
    address: {
      title: "Your personal forwarding address",
      loading: "Loading your address…",
      error: "Couldn't load your address. Check Settings (API base URL/token) and try again.",
      copy: "Copy",
      copied: "Copied",
      needsToken: "Set up your e-kariyerim access token in Settings first.",
      openSettings: "Open Settings",
    },
    steps: {
      title: "Set it up in Gmail — step by step",
      step1: {
        title: "1. Open Gmail's settings",
        body: "In Gmail, click the gear icon (top right), then \"See all settings.\"",
      },
      step2: {
        title: "2. Add your e-kariyerim address as a forwarding address",
        body: "Open the \"Forwarding and POP/IMAP\" tab → \"Add a forwarding address\" → paste the address above → Next → Proceed.",
      },
      step3: {
        title: "3. Confirm it — right here, no need to check another inbox",
        body: "Gmail emails a confirmation code to that address. You don't have access to that inbox (it's ours) — so we show the code here instead, as soon as it arrives.",
        waitingForCode: "No confirmation code yet. After completing step 2 in Gmail, click refresh below.",
        codeLabel: "Confirmation code:",
        refresh: "Refresh",
        openLink: "Open confirmation link",
        dismiss: "I've confirmed it in Gmail — dismiss",
      },
      step4: {
        title: "4. Turn on full forwarding — one time, no more editing",
        body: "Back on the \"Forwarding and POP/IMAP\" tab (same one as step 2), choose \"Forward a copy of incoming mail to\" your now-confirmed address → pick what happens to Gmail's own copy (we recommend keeping it in your inbox) → scroll down and click \"Save Changes.\" You only do this once — a new job application never needs a new filter.",
      },
      step5: {
        title: "5. Done — review suggestions as they arrive",
        body: "Forwarded emails that look like a status update show up as suggestions you approve — nothing changes on your applications automatically.",
        viewSuggestions: "View pending suggestions on ekariyerim.com →",
      },
    },
    faq: {
      title: "Common questions",
      q1: "Do you read my whole inbox?",
      a1: "Not exactly. Every incoming email is relayed to us once you turn forwarding on, but our system only acts on mail from known job platforms or companies you've already tracked in e-kariyerim — **everything else is discarded automatically: never stored, never used for anything else, never reviewed by anyone.**",
      q2: "Can I stop this?",
      a2: "Yes, anytime — just delete the filter and/or forwarding address in Gmail's own settings. Nothing needs to change on our side.",
      q3: "What exactly gets stored?",
      a3: "The subject line and roughly the first part of the body text of forwarded emails, tied to your account — used only to suggest application status updates.",
    },
    language: "Language",
  },
  tr: {
    pageTitle: "e-kariyerim — Mail Yönlendirme Kurulumu",
    backToSettings: "← Ayarlara dön",
    hero: {
      title: "Gelen kutunuzu otomatik statü güncellemesine dönüştürün",
      body: "Başvuru maillerinizi (mülakat daveti, ret, statü güncellemesi) kişisel e-kariyerim adresinize yönlendirin — onayınızı bekleyen bir öneri olarak karşınıza çıksınlar. Her başvuruyu elle güncellemenize gerek kalmaz.",
      trustTitle: "Başlamadan önce, kısaca:",
      trust1: "Mail hesabınıza asla giriş yapmayız. Gmail şifrenizi veya bir OAuth izni asla istemeyiz.",
      trust2: "Yönlendirmeyi bir kez, kendi Gmail ayarlarınızda açarsınız — sonrasında gelen her e-posta otomatik olarak bize iletilir. Gönderen bazlı bir filtre kurup sürdürmenize gerek yok.",
      trust3: "Yönlendirilen bir maili yalnızca bilinen bir ilan sitesinden/ATS'den ya da e-kariyerim'e zaten eklediğiniz bir şirketten geldiğinde bir öneriye dönüştürürüz — **geri kalan her şey anında elenir: hiçbir amaçla saklanmaz, kullanılmaz, kimse tarafından görülmez.**",
      trust4: "İstediğiniz zaman Gmail'in kendi \"Yönlendirme ve POP/IMAP\" ayarlarından yönlendirmeyi kapatarak geri alabilirsiniz. Bizim tarafımızda hiçbir şeyin değişmesi gerekmez.",
    },
    flow: {
      title: "Nasıl çalışır",
      step1: "Gmail gelen kutunuz",
      step2: "Her şeyi yönlendir (tek seferlik)",
      step3: "Kişisel e-kariyerim adresiniz",
      step4: "Sadece konu + kısa bir özet okuruz",
      step5: "Öneriyi siz onaylarsınız",
    },
    address: {
      title: "Kişisel yönlendirme adresiniz",
      loading: "Adresiniz yükleniyor…",
      error: "Adresiniz yüklenemedi. Ayarları (API adresi/anahtar) kontrol edip tekrar deneyin.",
      copy: "Kopyala",
      copied: "Kopyalandı",
      needsToken: "Önce Ayarlar'dan e-kariyerim erişim anahtarınızı kurun.",
      openSettings: "Ayarları Aç",
    },
    steps: {
      title: "Gmail'de kurulum — adım adım",
      step1: {
        title: "1. Gmail ayarlarını açın",
        body: "Gmail'de sağ üstteki dişli simgesine, ardından \"Tüm ayarları görüntüle\"ye tıklayın.",
      },
      step2: {
        title: "2. e-kariyerim adresinizi yönlendirme adresi olarak ekleyin",
        body: "\"Yönlendirme ve POP/IMAP\" sekmesini açın → \"Bir yönlendirme adresi ekle\" → yukarıdaki adresi yapıştırın → İleri → Devam Et.",
      },
      step3: {
        title: "3. Onaylayın — başka bir kutuya bakmanıza gerek yok, burada",
        body: "Gmail o adrese bir onay kodu gönderir. O kutuya erişiminiz yok (o kutu bize ait) — bu yüzden kod geldiği an burada gösteriyoruz.",
        waitingForCode: "Henüz bir onay kodu yok. Gmail'de 2. adımı tamamladıktan sonra aşağıdan yenileyin.",
        codeLabel: "Onay kodu:",
        refresh: "Yenile",
        openLink: "Onay bağlantısını aç",
        dismiss: "Gmail'de onayladım — kapat",
      },
      step4: {
        title: "4. Tüm yönlendirmeyi açın — tek seferlik, bir daha dokunmanıza gerek yok",
        body: "Aynı \"Yönlendirme ve POP/IMAP\" sekmesine (2. adımdakiyle aynı) dönün → \"Gelen postanın bir kopyasını şuraya yönlendir\" seçeneğini işaretleyip onaylanmış adresinizi seçin → Gmail'in kendi kopyasına ne olacağını belirleyin (önerimiz: gelen kutunuzda tutmak) → aşağı inip \"Değişiklikleri Kaydet\"e tıklayın. Bunu yalnızca bir kez yaparsınız — yeni bir başvuruda tekrar filtre kurmanıza gerek kalmaz.",
      },
      step5: {
        title: "5. Tamamlandı — önerileri geldikçe inceleyin",
        body: "Statü güncellemesine benziyorsa, yönlendirilen mailler onayınızı bekleyen bir öneri olarak görünür — başvurularınızda hiçbir şey otomatik değişmez.",
        viewSuggestions: "ekariyerim.com'da bekleyen önerileri görüntüle →",
      },
    },
    faq: {
      title: "Sık sorulan sorular",
      q1: "Tüm gelen kutumu mu okuyorsunuz?",
      a1: "Tam olarak değil. Yönlendirmeyi açtığınızda gelen her e-posta bize iletilir, ancak sistemimiz yalnızca bilinen ilan platformlarından ya da e-kariyerim'de zaten takip ettiğiniz şirketlerden gelen postalarla ilgilenir — **geri kalanı anında elenir: hiçbir amaçla saklanmaz, kullanılmaz, kimse tarafından görülmez.**",
      q2: "Bunu durdurabilir miyim?",
      a2: "Evet, istediğiniz zaman — Gmail'in kendi ayarlarından filtreyi ve/veya yönlendirme adresini silmeniz yeterli. Bizim tarafımızda hiçbir şeyin değişmesi gerekmez.",
      q3: "Tam olarak ne saklanıyor?",
      a3: "Yönlendirilen maillerin konu satırı ve gövde metninin yaklaşık ilk kısmı, hesabınıza bağlı olarak — sadece başvuru statü güncellemesi önermek için kullanılır.",
    },
    language: "Dil",
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
