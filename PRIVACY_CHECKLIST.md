# KVKK / GDPR Self-Review Checklist

**Mühendislik self-review checklist'i, hukuki tavsiye değildir.** Spec
§30'daki maddeleri, bugüne kadar inşa edilen özelliklere göre
değerlendirir. Yeni bir özellik (email entegrasyonu, public analytics
vb.) eklendiğinde bu liste güncellenmeli.

## Yapıldı

| Madde | Nasıl karşılandı |
|---|---|
| Privacy policy | `web/src/app/privacy/page.tsx` — kayıt formundan ve `/settings`'ten link veriliyor. |
| Explicit consent | `ApplicationUser.ConsentAcceptedAt`, kayıtta backend'de set ediliyor; frontend checkbox olmadan submit'i engelliyor (bkz. `registerSchema.ts`, `RegisterRequestValidator`). |
| Account deletion | `DELETE /api/users/me` (`AuthService.DeleteAccountAsync`) — şifre onayı gerektirir, hesabın tüm verisini kalıcı olarak siler. `web/src/app/(protected)/settings/page.tsx`'ten erişilir. |
| Data deletion | Hesap silme ile aynı — bkz. yukarıdaki `DECISIONS.md` "Sprint 7" bölümündeki cascade sırası. |
| Data export | `GET /api/users/me/export` (`AuthService.ExportAccountDataAsync`) — profil, başvurular (+events/status history), import batch'leri, reminder'lar JSON olarak indirilebilir. |
| Rate limiting / abuse resistance | `src/AfterApply.Api/RateLimiting.cs` — auth endpoint'leri (IP bazlı) ve upload endpoint'leri (kullanıcı bazlı) korumalı. |
| Upload boyut/zip-bomb koruması | `ImportOptions` (dosya/ZIP boyutu, satır sayısı, entry sayısı limitleri) + `LimitedStream` (decompression sırasında byte-cap). |
| Data deletion cascade doğruluğu | Şirket/iş ilanı verisi paylaşımlı olduğu için hesap silmede asla silinmiyor — entegrasyon testiyle doğrulandı (`AccountManagementTests.DeleteAccount_Cascades_...`). |

## Henüz uygulanamaz (özellik yok) — N/A

| Madde | Neden |
|---|---|
| Public analytics anonimleştirme / minimum örneklem | Public/şirket-görünür analytics (Phase 10+) flag ile kapalı (`CompanyIntelligence:Enabled=false`); `IAnalyticsService` bugün sadece kullanıcının kendi verisini kendisine döndürüyor, hiçbir agregasyon başkasına gösterilmiyor. |

> **Not (2026-08-29):** Bu listede daha önce "Email permission boundaries — MVP kapsamında değil" satırı vardı. **Artık doğru değil** — Gmail entegrasyonu (Sprint 9, `gmail.readonly` scope) tamamlanıp canlıya alındı. Bkz. aşağıdaki envanterin 4. maddesi; bu artık N/A değil, avukata görülmesi gereken açık bir kalem.

## Kabul edilen risk / backlog

| Madde | Not |
|---|---|
| Token storage (`localStorage`) | Access/refresh token'lar httpOnly cookie yerine `localStorage`'da tutuluyor (`web/src/lib/api/tokenStorage.ts`) — önceki sprintlerden gelen mimari tercih. Değiştirmek `AuthContext`/`authStore`/`tokenStorage`/her `apiFetch` çağrısını etkileyen büyük bir refactor; bu sprintin kapsamında istenmedi, backlog'a not düşüldü. |
| Consent versiyonlama / re-consent | `ConsentAcceptedAt` sadece kayıt anında bir kere set ediliyor; privacy policy içerik olarak değişirse mevcut kullanıcılardan yeniden onay istenmesi gereken bir akış yok. Backlog. |
| Hangfire dashboard auth | `/hangfire` bu sprintte de eklenmedi (Sprint 6'da ertelendi) — hâlâ backlog. |

---

## Avukata götürülecek envanter ve eksikler (2026-08-29)

**Amaç:** Bu bölüm hukuki tavsiye değil — kod taranarak çıkarılmış, gerçek bir
KVKK/GDPR danışmanına/avukata götürülecek ham envanter + eksik listesi.
Nihai onay/metin yazımı bu dosyanın kapsamı dışında.

### Veri akışı envanteri (hangi veri nerede/kime gidiyor)

| Veri | Nerede saklanıyor | Üçüncü tarafa gidiyor mu |
|---|---|---|
| Hesap (email, şifre hash'i, `ConsentAcceptedAt`) | Cloud SQL (Postgres, `europe-west1`) | Hayır |
| Başvuru verisi (Company/Job/Application/Event/StatusHistory) | Cloud SQL, `europe-west1` | Hayır |
| CV / profil metni (`CandidateProfile.CvText`, `JobMatch.CvTextSnapshot`) | Cloud SQL, düz metin (şifrelenmemiş) | **Evet — OpenAI API'ye** (match hesaplama için CV + job description gönderiliyor, ABD merkezli, yurt dışı aktarım) |
| Gmail bağlantısı | OAuth token (encrypted) DB'de; `gmail.readonly` scope — **kullanıcının tüm gelen kutusunu okuma izni**, sadece "application-related" filtrelemesi kod tarafında yapılıyor, Google API seviyesinde kısıtlı değil | Google'a (OAuth flow) — veri Google'da zaten var, biz sadece okuyoruz |
| Email eşleştirme sonucu (`EmailSuggestion`) | Cloud SQL — sadece `messageId`/`threadId`/`senderDomain`/`matchedRule`/`confidenceScore` persist ediliyor, **e-posta konusu/içeriği (Subject/Snippet) DB'ye yazılmıyor**, sadece eşleştirme anında bellekte kullanılıp atılıyor | Hayır (iyi durumda — data minimization) |
| Personal Access Token (extension) | Sadece hash (`TokenHash`) DB'de, düz metin token bir daha gösterilmiyor | Hayır |
| Extension'ın scrape ettiği veri (LinkedIn/kariyer.net job title/company/location/description/URL) | Cloud SQL (Application/Job/Company) | Hayır |
| Hata/performans telemetrisi | Sentry (backend `Sentry.AspNetCore`, frontend `@sentry/nextjs`) — konfigürasyon varsayılan (`sendDefaultPii` açıkça ayarlanmamış), hangi alanların gittiği doğrulanmadı | **Evet — Sentry'ye** (SaaS, muhtemelen ABD/AB veri merkezi seçimine bağlı, hesap ayarları kontrol edilmeli) |
| Tüm altyapı (Cloud Run ×2, Cloud SQL, Memorystore) | Google Cloud, `europe-west1` (AB bölgesi) | Google Cloud (alt-işlemci) |

### Eksik / avukata sorulması gereken kalemler

1. **Aydınlatma Metni (KVKK m.10 formatı)** — `/privacy` sayfası var ama informal; m.10'un istediği spesifik başlıklar (veri sorumlusu kimliği, işlenen veri kategorileri, işleme amaçları, aktarılan alıcı grupları, toplama yöntemi/hukuki sebebi, m.11 hakları) format olarak karşılanmıyor.
2. **Granüler açık rıza yok** — kayıtta tek genel checkbox. CV'nin OpenAI'a gönderilmesi ve Gmail okuma izni için ayrı, spesifik rıza/bilgilendirme adımı yok (ikisi de şu an "genel privacy policy'yi kabul ettin" şemsiyesi altında).
3. **Yurt dışı aktarım disclosure'ı yok** — OpenAI ve Sentry hiçbir yerde isim olarak geçmiyor; KVKK'nın 2024 değişikliğiyle gelen yurt dışı aktarım rejimi (açık rıza veya yeterlilik kararı/SCC) hangisine dayanacağımız netleşmemiş.
4. **ToS / Kullanım Şartları** — hiç yok.
5. **VERBİS kaydı** — muafiyet kapsamına girip girmediğimiz (ölçek/ana faaliyet kriterleri) teyit edilmemiş.
6. **Çerez Politikası** — yok. `NEXT_LOCALE` ve theme cookie'leri fonksiyonel/zorunlu görünüyor ama yine de disclosure gerekir.
7. **Gmail API "restricted scope" platform uyumu** — `gmail.readonly`, Google'ın kendi CASA güvenlik değerlendirmesi + OAuth consent screen doğrulamasını gerektirebilir (100 kullanıcıyı aşınca zorunlu hâle geliyor). Bu KVKK değil ama gerçek bir platform-compliance riski — ayrı takip edilmeli.
8. **Özel nitelikli veri riski** — CV serbest metin olarak alınıyor, hiç filtrelenmiyor; kullanıcı istemeden sağlık/din/sendika üyeliği gibi özel nitelikli veri girebilir. Avukata bu riskin nasıl yönetileceği (ek uyarı metni, vb.) sorulmalı.
9. **Consent versioning** — zaten yukarıda backlog'da not düşülmüş, hâlâ açık.

### Olumlu / avukatın işini kolaylaştıran noktalar

- E-posta içeriği (subject/snippet) hiç persist edilmiyor — sadece metadata tutuluyor.
- PAT'lar hash'li saklanıyor, düz metin yok.
- Hesap silme + veri export zaten uçtan uca çalışıyor ve test edilmiş.
- Tüm altyapı AB bölgesinde (`europe-west1`).
- Rate limiting ve zip-bomb koruması mevcut.
