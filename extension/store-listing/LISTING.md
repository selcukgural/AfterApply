# Chrome Web Store listing copy

Fill these into the Developer Dashboard's "Store listing" tab. Two language variants are given
(English and Turkish) — add both under **Store listing → Languages** so the listing localizes for
Turkish users automatically; Chrome shows whichever matches the visitor's browser language.

## Item name

```
e-kariyerim — Job Import
```

Matches `manifest.json`'s `name` field; the Web Store requires these to be identical.

## Summary (short description, max 132 characters)

**EN** (130 chars)
```
Get your job applications into e-kariyerim — one click from LinkedIn/kariyer.net, or by forwarding status emails. No copy-pasting.
```

**TR** (122 chars)
```
Başvurularınızı e-kariyerim'e aktarın — LinkedIn/kariyer.net'te tek tıkla, ya da mail yönlendirerek. Kopyala-yapıştır yok.
```

## Detailed description

**EN**
```
e-kariyerim — Job Import turns a LinkedIn or kariyer.net job posting into a tracked application
in your e-kariyerim account, in one click.

HOW IT WORKS
1. Open a job posting on LinkedIn (a /jobs/view/ page, or a job selected in search results) or on
   kariyer.net (an /is-ilani/ page).
2. Click the e-kariyerim icon in your toolbar.
3. The popup reads the job title, company, and location straight off the page and fills them in
   for you — every field stays editable before you submit, so an imperfect read never becomes a
   wrong entry.
4. Click "I Applied." Done — it's now tracked in e-kariyerim, matched to the job description for
   later reference.

Clicking it again on the same posting is safe: e-kariyerim recognizes the job by its URL and opens
your existing application instead of creating a duplicate.

EMAIL FORWARDING (NEW)
Not every status update happens on a job site — most arrive by email. The extension includes a
full, step-by-step setup guide for forwarding those emails (interview invites, rejections, status
updates) to your own personal e-kariyerim address, so they show up as suggestions you approve
instead of updates you have to make by hand.

Open it from the extension's Settings page ("Set up Email Forwarding") — one click away from the
main popup too, via the gear icon. It walks you through adding your address as a forwarding
destination in Gmail's own settings, then turning on Gmail's native "forward a copy of incoming
mail" — a one-time step, so a new job application never needs new setup. Everything forwarded is
relayed to us automatically after that, but we only ever turn an email into something you see when
it's from a known job platform or a company you've already tracked in e-kariyerim — everything
else is discarded immediately, never stored, never shown to anyone. Nothing is automated on
Gmail's side: you turn forwarding on (and can turn it off again) entirely inside your own account.

GMAIL SCANNING (BETA, OPT-IN)
An alternative to email forwarding, for anyone who'd rather not relay their whole inbox anywhere.
Turn it on in Settings and, from then on, whenever you open an email in Gmail yourself, the
extension reads that one message in your own browser and checks — locally, on your device —
whether it looks like a job-application update. Only if it does, a small extracted summary
(sender, subject, and a short snippet — never the full email, never anything about messages you
didn't open) is sent to your e-kariyerim account as a suggestion. Off by default; nothing is read
or sent unless you turn it on in Settings.

LANGUAGE
The whole extension — the job-tracking popup, Settings, and the email-forwarding guide — works in
Turkish or English. Switch anytime with the language toggle in any page's header.

REQUIREMENTS
You need an e-kariyerim account (ekariyerim.com) and a personal access token, generated from
Settings → Browser Extension inside the app. The extension does nothing until you paste that token
into its Settings page.

PRIVACY
The extension only reads a job page when you click its icon, only on linkedin.com and kariyer.net,
and only sends the fields you see in the popup — to your own e-kariyerim account, using your own
token. The email-forwarding guide only reads your own forwarding address and confirmation code
from your e-kariyerim account (same token) and links out to Gmail's own settings pages — it never
signs in to, or reads, your email account itself. Gmail Scanning, described above, is off by
default and reads only a message you've personally opened, only after you enable it in Settings;
the relevance check runs on your device, and only a short extracted summary of a message that
looks job-related is ever sent, never the raw email. Nothing is sent to any third party, and
nothing is used for advertising. See the full privacy policy linked on this listing.

This is an independent tool and is not affiliated with, endorsed by, or sponsored by LinkedIn
Corporation, kariyer.net, or Google.
```

**TR**
```
e-kariyerim — Job Import, LinkedIn veya kariyer.net'teki bir iş ilanını tek tıkla e-kariyerim
hesabınıza kaydedilmiş bir başvuruya dönüştürür.

NASIL ÇALIŞIR
1. LinkedIn'de bir iş ilanı açın (/jobs/view/ sayfası veya arama sonuçlarında seçili bir ilan) ya
   da kariyer.net'te bir ilan açın (/is-ilani/ sayfası).
2. Araç çubuğundaki e-kariyerim simgesine tıklayın.
3. Açılan pencere; ilan başlığını, şirket adını ve konumu doğrudan sayfadan okuyup sizin için
   doldurur — göndermeden önce tüm alanlar düzenlenebilir, böylece eksik bir okuma asla yanlış bir
   kayda dönüşmez.
4. "Başvurdum"a tıklayın. Bu kadar — başvurunuz artık e-kariyerim'de, ileride bakmak üzere ilan
   metniyle birlikte kayıtlı.

Aynı ilanda tekrar tıklamak güvenlidir: e-kariyerim ilanı URL'sinden tanır ve yinelenen bir kayıt
oluşturmak yerine mevcut başvurunuzu açar.

MAIL YÖNLENDİRME (YENİ)
Her statü güncellemesi bir iş sitesinde olmuyor — çoğu mail ile geliyor. Eklenti bu mailleri
(mülakat daveti, ret, statü güncellemesi) kişisel e-kariyerim adresinize yönlendirmeniz için tam,
adım adım bir kurulum rehberi içeriyor — böylece elle güncellemeniz gereken bir şey yerine,
onayınızı bekleyen bir öneri olarak karşınıza çıkıyorlar.

Eklentinin Ayarlar sayfasından açın ("Mail Yönlendirme Kur") — ana penceredeki dişli simgesiyle de
bir tık uzakta. Adresinizi Gmail'in kendi ayarlarında bir yönlendirme adresi olarak eklemekten,
Gmail'in kendi "gelen postanın bir kopyasını yönlendir" özelliğini açmaya kadar sizi adım adım
yönlendirir — tek seferlik bir işlem, yeni bir başvuruda tekrar kurulum gerekmez. Bundan sonra
gelen her e-posta otomatik olarak bize iletilir, ancak bir maili yalnızca bilinen bir ilan
sitesinden/ATS'den ya da e-kariyerim'e zaten eklediğiniz bir şirketten geldiğinde bir şeye
dönüştürürüz — geri kalanı anında elenir, asla saklanmaz, kimseye gösterilmez. Gmail tarafında
hiçbir şey otomatik yapılmaz: yönlendirmeyi tamamen kendi hesabınızda siz açar (ve istediğinizde
kapatırsınız).

GMAIL TARAMASI (BETA, OPTIONAL)
Tüm gelen kutunuzu hiçbir yere yönlendirmek istemeyenler için mail yönlendirmeye bir alternatif.
Ayarlar'dan açtıktan sonra, Gmail'de kendiniz bir mail açtığınızda eklenti o tek maili kendi
tarayıcınızda okur ve — cihazınızda, yerel olarak — bir iş başvurusu güncellemesine benzeyip
benzemediğine bakar. Yalnızca benziyorsa, küçük bir özet (gönderen, konu ve kısa bir alıntı —
asla mailin tamamı, asla açmadığınız mailler hakkında hiçbir şey) e-kariyerim hesabınıza bir öneri
olarak gönderilir. Varsayılan olarak kapalıdır; Ayarlar'dan açmadığınız sürece hiçbir şey okunmaz
veya gönderilmez.

DİL
Eklentinin tamamı — başvuru takip penceresi, Ayarlar ve mail yönlendirme rehberi — Türkçe veya
İngilizce çalışır. Herhangi bir sayfanın başlığındaki dil butonuyla istediğiniz zaman değiştirin.

GEREKSİNİMLER
Bir e-kariyerim hesabına (ekariyerim.com) ve uygulama içindeki Ayarlar → Tarayıcı Eklentisi
bölümünden oluşturacağınız bir erişim anahtarına ihtiyacınız var. Bu anahtarı eklentinin Ayarlar
sayfasına yapıştırana kadar eklenti hiçbir şey yapmaz.

GİZLİLİK
Eklenti bir ilan sayfasını yalnızca simgesine tıkladığınızda, yalnızca linkedin.com ve kariyer.net
üzerinde okur ve yalnızca açılan pencerede gördüğünüz alanları — kendi erişim anahtarınızla,
yalnızca kendi e-kariyerim hesabınıza gönderir. Mail yönlendirme rehberi yalnızca kendi
e-kariyerim hesabınızdaki (aynı anahtarla) yönlendirme adresinizi ve onay kodunuzu okur, Gmail'in
kendi ayarlar sayfalarına bağlantı verir — mail hesabınıza asla giriş yapmaz veya mailinizi
okumaz. Yukarıda anlatılan Gmail Taraması varsayılan olarak kapalıdır ve yalnızca Ayarlar'dan
etkinleştirdikten sonra, yalnızca kendinizin açtığı bir maili okur; ilgililik kontrolü cihazınızda
çalışır ve yalnızca iş başvurusuyla ilgili göründüğünde kısa bir özet gönderilir, ham mail asla
gönderilmez. Hiçbir veri üçüncü taraflarla paylaşılmaz veya reklam amacıyla kullanılmaz.
Ayrıntılar için bu listede bağlantısı verilen gizlilik politikasına bakın.

Bu bağımsız bir araçtır; LinkedIn Corporation, kariyer.net veya Google ile bağlantılı, onlar
tarafından onaylanmış veya desteklenmiş değildir.
```

## Category

`Productivity`

## Language

English (add Turkish as a listing translation, see above)

## Store icon

`icons/icon128.png` (already in the extension package — the Dashboard re-uses it, no separate
upload needed unless you want a distinct store-only icon).

## Screenshots

`screenshots/popup-light.png`, `screenshots/popup-dark.png`, `screenshots/options-light.png`,
`screenshots/forwarding-light.png`, `screenshots/forwarding-dark.png` — 1280×800 PNG, ready to
upload as-is. Chrome Web Store requires at least one and allows up to five; these five cover the
three things this listing needs to show (auto-fill in both themes, the one-time settings setup,
and the email-forwarding guide). See `screenshots/README.md` if you want to regenerate or add
more. **`options-light.png` is now stale** — the Settings page gained a new "Gmail Scanning"
checkbox/label section below the email-forwarding button — regenerate it before upload if you want
the screenshot to match the live page (not blocking, since the old version is still an accurate
screenshot of the token/forwarding parts of that page, just incomplete).

## Support / website links

- Website: `https://ekariyerim.com`
- Support email: use the account contact address configured in the Developer Dashboard (Chrome
  requires a support email visible on the listing).

## Privacy policy URL

Publish `PRIVACY_POLICY.md`'s content at a real URL before submitting — e.g.
`https://ekariyerim.com/privacy` — and enter that URL in **Store listing → Privacy practices →
Privacy policy**. The Dashboard rejects submissions that handle authentication data (this extension
stores a personal access token) without one.
