# PromoVideo — senaryodan tanıtım videosu

Bir JSON senaryo dosyası yazarsın, araç şunları yapar:

1. Her sahnenin anlatım metnini yapay zekâ sesiyle okutur (Türkçe/İngilizce).
2. Görünmez bir Chrome açar, yerel e-kariyerim'de demo hesapla oturum açar (bu kısım kayda girmez).
3. Sahne sahne sayfalara gider, tıklar, yazar, kaydırır. Ekranda hareket eden bir imleç ve
   tıklama efekti olur. Her sahne en az anlatım süresi kadar sürer.
4. Açılış/kapanış kartlarını (logo + başlık) sayfanın üstüne çizer.
5. ffmpeg ile görüntüyü, sesi, arka plan müziğini ve altyazıyı birleştirip YouTube'a hazır bir
   MP4 üretir. Müzik, anlatım başlayınca kendiliğinden kısılır; ses YouTube'un çaldığı
   seviyeye (−16 LUFS) ayarlanır.

Kamera, mikrofon ya da kurgu programı gerekmez. Arayüz değişince aynı komut videoyu yeniden çeker.

## Gereksinimler

- Google Chrome (başka bir yoldaysa `CHROME_PATH`)
- `ffmpeg` (`brew install ffmpeg`)
- Yerel stack: API `:5151` + web `:3000` (kök README, "Running locally"). **Yayınlanacak
  render için web'i üretim modunda çalıştır** (`cd web && npm run build && npm run start`):
  geliştirme sunucusu köşeye Next.js simgesini basar ve sayfalar daha yavaş açılır.
- Oturumlu senaryolar için demo hesabın bilgileri ortam değişkeninde:
  `PROMO_EMAIL`, `PROMO_PASSWORD`. Şifre repoya ve senaryo dosyasına **yazılmaz**.

## Komutlar

```bash
# Senaryoyu kontrol et (yazım hatası, eksik alan, bilinmeyen adım)
dotnet run --project tools/PromoVideo -- validate tools/PromoVideo/scenarios/genel-tanitim-tr.json

# Yalnızca sesi üret ve dinle: metni ve temposunu kayıt yapmadan dene
dotnet run --project tools/PromoVideo -- voice tools/PromoVideo/scenarios/genel-tanitim-tr.json

# Videoyu üret
PROMO_EMAIL=... PROMO_PASSWORD=... \
  dotnet run --project tools/PromoVideo -- render tools/PromoVideo/scenarios/genel-tanitim-tr.json

# Google'daki Türkçe sesleri listele
dotnet run --project tools/PromoVideo -- voices --language tr-TR
```

`render` seçenekleri:

| Seçenek | Ne yapar |
|---|---|
| `--burn-captions` | Altyazıyı görüntünün içine basar (sessiz oynatılan Shorts/akış için) |
| `--show-browser` | Chrome'u görünür açar; senaryo yazarken ne olduğunu izlemek için |
| `--keep-frames` | Ham kareleri silmez (hata ayıklama) |
| `--provider`, `--voice`, `--model`, `--rate` | Senaryodaki ses ayarını dosyayı değiştirmeden ezer |

## Çıktı

`artifacts/promo-video/<senaryo-adı>/` (git'e girmez):

| Dosya | Ne işe yarar |
|---|---|
| `video.mp4` | 1080p H.264 + AAC; içinde açılıp kapanabilen altyazı izi de var |
| `subtitles.srt` | YouTube Studio › Altyazılar › Dosya yükle (zamanlamalı) |
| `chapters.txt` | Video açıklamasına yapıştır; YouTube bölümleri olur |
| `narration.wav` | Yalnızca ses izi |
| `failed-step.png` | Bir adım başarısız olursa o anki sayfa |

Seslendirilen parçalar `artifacts/promo-video/.voice-cache/` altında önbelleklenir. Metni
değişmeyen sahne yeniden seslendirilmez, ücretli bir ses bir kez ödenir.

## Ses seçenekleri

| Sağlayıcı | Ne zaman | Not |
|---|---|---|
| `say` (varsayılan) | Taslak, zamanlama denemesi | macOS'un "Yelda" sesi, çevrimdışı, anında. **Yayın için değil:** Apple'ın lisansı sistem seslerini kişisel ve ticari olmayan kullanımla sınırlar. |
| `google` | **Yayınlanacak video** | Google Cloud Text-to-Speech. Doğal Türkçe sesler, Cloud şartlarında ticari kullanım serbest, aylık ücretsiz kota birkaç videoyu rahat karşılar (güncel rakamlar fiyat sayfasında). `gcloud` oturumunu kullanır, anahtar dosyası oluşturmaz. Projede Text-to-Speech API açık olmalı. Proje `GOOGLE_CLOUD_PROJECT` ya da `gcloud config` üzerinden gelir. `voice.model` verilirse Gemini-TTS kullanılır (ör. `gemini-2.5-pro-tts`; ses adı yalnızca `Charon`, `Kore` gibi); bu modeller cümlenin anlamından okur, ekli kısaltmalarda daha doğaldır ve `voice.prompt` ile düz cümleyle bir okuma tarzı alır. |
| `elevenlabs` | Yayınlanacak video, alternatif | ElevenLabs. `voice.name` bir ses kimliğidir (voice id), `voice.model` model kimliği (varsayılan `eleven_multilingual_v2`, daha canlı ama daha az öngörülebilir olan `eleven_v3`). Anahtar `ELEVENLABS_API_KEY` ortam değişkeninden okunur, hiçbir yere yazılmaz; yalnızca Text to Speech yetkili bir anahtar yeterlidir. `rate` 0.7–1.2 arasıdır. **Ücretsiz plan ticari değildir ve atıf ister**; yayın için ücretli plan gerekir, güncel şartlara bak. |
| `piper` | Çevrimdışı, açık kaynak | `piper` komutu + `.onnx` ses modeli. Her modelin lisansı ayrıdır; yayından önce model kartına bak. |

Taslağı `say` ile yazıp aynı senaryoyu Google sesiyle yayınlamak için:

```bash
dotnet run --project tools/PromoVideo -- render tools/PromoVideo/scenarios/genel-tanitim-tr.json --provider google --voice <ses-adı>
```

(Ses adını `voices` komutundan seç.)

## Müzik

- `"music": { "generate": "ambient", "volume": 0.18 }`: araç kendi sakin ambient altlığını
  besteler (C majörde I–V–vi–IV pad, yumuşak bas, yavaş arpej). **Bizim ürettiğimiz ses olduğu
  için telif ya da Content ID riski yoktur.** Aynı uzunluk her seferinde aynı parçayı verir.
- `"music": { "file": "parca.mp3", "volume": 0.2 }`: kendi parçan, örneğin YouTube Studio ›
  Ses Kitaplığı'ndan indirdiğin, YouTube'da ücretsiz kullanılabilen bir parça ("atıf gerekmez"
  olanları seç). Video boyunca döngüye alınır ya da kesilir, başı ve sonu yumuşatılır.
- İki durumda da müzik anlatımın altında kısılır (sidechain) ve boşluklarda geri gelir.

## Telaffuz

TTS, "CV" gibi Türkçede İngilizce okunan kısaltmaları harf harf okur. `pronounce` sözlüğü
yalnızca **sese giden metni** değiştirir, altyazı yazıldığı gibi kalır:

```jsonc
"pronounce": { "CV": "sivi", "ATS": "ey ti es", "LinkedIn": "Linktin", "ekariyerim.com": "e kariyerim nokta kom" }
```

Değişiklik yalnızca kelimenin tamamında yapılır: "CV'ni" → "sivi'ni" olur, "CVS" değişmez.
En uzun eşleşme önce uygulanır. Yeni bir kelimenin nasıl okunduğunu `voice` komutuyla dinleyip
yazımı ona göre ayarla.

## Senaryo dosyası

```jsonc
{
  "title": "e-kariyerim tanıtım",
  "language": "tr",                        // tr | en: URL öneki ve sesin dili
  "baseUrl": "http://localhost:3000",
  "viewport": { "width": 1440, "height": 810, "deviceScaleFactor": 2 },  // tarayıcı boyutu
  "output":   { "width": 1920, "height": 1080, "fps": 30 },              // video boyutu
  "colorScheme": "light",                   // light | dark
  "signIn": true,                           // yalnızca yerel adreslerde izinli
  "voice": { "provider": "google", "name": "tr-TR-Chirp3-HD-Charon", "rate": 1.0 },
  "music": { "generate": "ambient", "volume": 0.18 },
  "pronounce": { "CV": "sivi" },
  "hide": ["button.fixed.bottom-4.right-4"],  // kayıtta gizlenecek öğeler
  "scenes": [
    {
      "id": "panel",                        // benzersiz
      "chapter": "Panel",                   // YouTube bölüm başlığı (isteğe bağlı)
      "narration": "Panelde açık başvurularını bir bakışta görürsün.",
      "leadIn": 0.4,                        // sahne başı → ses başı (sn)
      "tail": 0.8,                          // ses sonu → sonraki sahne (sn)
      "minSeconds": null,                   // sessiz sahnede zorunlu
      "steps": [
        { "action": "goto", "path": "/tr/dashboard" },
        { "action": "scroll", "by": 500 }
      ]
    }
  ]
}
```

Adımlar (`target` bir CSS seçicisi ya da görünen metinle `text=Pano` olabilir):

| Adım | Alanlar |
|---|---|
| `goto` | `path`: sitede bir yol, `/` ile başlar (başka siteye gidilemez) |
| `click` | `target` |
| `hover` | `target` |
| `type` | `target`, `text`, `delayMs` (harf arası, varsayılan 70) |
| `press` | `key`: Enter, Escape, Tab, ArrowDown, ArrowUp, Backspace |
| `scroll` | `target` (öğeye kaydır) **ya da** `by` (piksel) |
| `wait` | `seconds` |
| `waitFor` | `target`, `seconds` (zaman aşımı, varsayılan 10) |
| `card` | `title`, `text` (isteğe bağlı alt satır): logolu tam ekran kart, yumuşak açılır |
| `hideCard` | Kartı kapatır (bir sonraki `goto` da kapatır) |

Açılış kartı genelde sessiz bir sahnedir (`minSeconds` ile), kapanış kartında anlatım olabilir.

Dikey Shorts için: `"viewport": { "width": 390, "height": 844, "deviceScaleFactor": 3, "mobile": true }`,
`"output": { "width": 1080, "height": 1920 }` ve `--burn-captions`.

## Kurallar

- **Yalnızca demo hesap.** Oturumlu kayıt yalnızca yerel stack'e karşı çalışır (araç başka
  adreste oturum açmayı reddeder). Ekrana gerçek başvuru, İK kişisi ya da e-posta girmemeli.
- **Kişisel isim yok.** Anlatım "ekip" dilinde olmalı; demo hesabın adı kurgusal.
- Metinlerde ürün adı **e-kariyerim**.
- Senaryolar `scenarios/` altında. Birim testleri bu klasördeki her dosyanın geçerli olduğunu
  kontrol eder (`tests/AfterApply.UnitTests/PromoVideo`).
