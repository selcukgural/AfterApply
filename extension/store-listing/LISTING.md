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

**EN** (98 chars)
```
Track LinkedIn and kariyer.net job applications in e-kariyerim with one click. No copy-pasting.
```

**TR** (108 chars)
```
LinkedIn ve kariyer.net'teki iş ilanlarını tek tıkla e-kariyerim'e ekleyin. Kopyala-yapıştır yok.
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

REQUIREMENTS
You need an e-kariyerim account (ekariyerim.com) and a personal access token, generated from
Settings → Browser Extension inside the app. The extension does nothing until you paste that token
into its Settings page.

PRIVACY
The extension only reads the page when you click its icon, only on linkedin.com and kariyer.net,
and only sends the fields you see in the popup — to your own e-kariyerim account, using your own
token. Nothing is sent to any third party, and nothing is used for advertising. See the full
privacy policy linked on this listing.

This is an independent tool and is not affiliated with, endorsed by, or sponsored by LinkedIn
Corporation or kariyer.net.
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

GEREKSİNİMLER
Bir e-kariyerim hesabına (ekariyerim.com) ve uygulama içindeki Ayarlar → Tarayıcı Eklentisi
bölümünden oluşturacağınız bir erişim anahtarına ihtiyacınız var. Bu anahtarı eklentinin Ayarlar
sayfasına yapıştırana kadar eklenti hiçbir şey yapmaz.

GİZLİLİK
Eklenti sayfayı yalnızca simgesine tıkladığınızda, yalnızca linkedin.com ve kariyer.net üzerinde
okur ve yalnızca açılan pencerede gördüğünüz alanları — kendi erişim anahtarınızla, yalnızca kendi
e-kariyerim hesabınıza gönderir. Hiçbir veri üçüncü taraflarla paylaşılmaz veya reklam amacıyla
kullanılmaz. Ayrıntılar için bu listede bağlantısı verilen gizlilik politikasına bakın.

Bu bağımsız bir araçtır; LinkedIn Corporation veya kariyer.net ile bağlantılı, onlar tarafından
onaylanmış veya desteklenmiş değildir.
```

## Category

`Productivity`

## Language

English (add Turkish as a listing translation, see above)

## Store icon

`icons/icon128.png` (already in the extension package — the Dashboard re-uses it, no separate
upload needed unless you want a distinct store-only icon).

## Screenshots

`screenshots/popup-light.png`, `screenshots/popup-dark.png`, `screenshots/options-light.png` —
1280×800 PNG, ready to upload as-is. Chrome Web Store requires at least one and allows up to five;
these three cover the two things this listing needs to show (auto-fill in both themes, and the
one-time settings setup). See `screenshots/README.md` if you want to regenerate or add more.

## Support / website links

- Website: `https://ekariyerim.com`
- Support email: use the account contact address configured in the Developer Dashboard (Chrome
  requires a support email visible on the listing).

## Privacy policy URL

Publish `PRIVACY_POLICY.md`'s content at a real URL before submitting — e.g.
`https://ekariyerim.com/privacy` — and enter that URL in **Store listing → Privacy practices →
Privacy policy**. The Dashboard rejects submissions that handle authentication data (this extension
stores a personal access token) without one.
