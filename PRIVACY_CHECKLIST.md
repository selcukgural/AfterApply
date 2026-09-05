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
| ~~Granüler açık rıza (CV → OpenAI)~~ | ~~(2026-09-01)~~ **N/A (2026-09-02):** AI Job Matching özelliği (CV → OpenAI) ürün kapsamından tamamen kaldırıldı, bkz. `DECISIONS.md`. Bu madde artık uygulanamaz — özellik yok. |
| ~~Yurt dışı aktarım disclosure'ı (CV → OpenAI)~~ | ~~(2026-09-01)~~ **N/A (2026-09-02):** Aynı kaldırma ile `/privacy#cross-border-transfer` bölümü de kaldırıldı. |
| Yurt dışı aktarım disclosure'ı (Email → OpenAI) | (2026-09-02) `/privacy#cross-border-transfer` yeniden eklendi, bu kez `EmailIntegrations` özelliğine özgü: Mail Forwarding'i kuran kullanıcıların yönlendirdiği e-postaların Subject/Snippet'inin OpenAI'a (ABD) gönderildiği isimle anılıyor; amaç, hukuki sebep (sadece kullanıcının kurduğu forwarding), geri çekme yöntemi ve "tam e-posta içeriği değil, sadece subject/snippet" notu ayrı ayrı maddelendi. `dataCollection`'a bunu kapsayan bir `item4` eklendi. **Granüler bir onay kutusu eklenmedi** — bu, ayrı ele alınması gereken bir ürün kararı (bkz. Eksik #2). **Güncelleme (2026-09-03):** aynı disclosure (`item4` + `crossBorderTransfer`'ın tüm alt maddeleri), tarayıcı eklentisine eklenen ikinci, isteğe bağlı bir intake yolunu — Gmail Taraması (`EmailProvider.Extension`, bkz. aşağıdaki envanter) — da kapsayacak şekilde genişletildi. Aynı OpenAI sınıflandırma/extraction pipeline'ını paylaşıyor, bu yüzden aynı disclosure geçerli; tek fark Gmail Taraması'nda içeriğin önce cihazda (tarayıcıda) yerel olarak elenmesi — e-kariyerim sunucularına ulaşmadan önce bile bir filtre daha var, Mail Forwarding'de olmayan bir ek koruma katmanı. Granüler onay kutusu eksikliği (Eksik #2) bu ikinci yol için de aynen geçerli. |

## Henüz uygulanamaz (özellik yok) — N/A

| Madde | Neden |
|---|---|
| Public analytics anonimleştirme / minimum örneklem | Public/şirket-görünür analytics (Phase 10+) flag ile kapalı (`CompanyIntelligence:Enabled=false`); `IAnalyticsService` bugün sadece kullanıcının kendi verisini kendisine döndürüyor, hiçbir agregasyon başkasına gösterilmiyor. |
| Granüler açık rıza / yurt dışı aktarım disclosure'ı (CV → OpenAI) | AI Job Matching özelliği 2026-09-02'de ürün kapsamından tamamen kaldırıldı — CV metni artık hiçbir yere (OpenAI dahil) gönderilmiyor, bu iki madde bir daha geçerli olana kadar N/A. |

> **Not (2026-08-29):** Bu listede daha önce "Email permission boundaries — MVP kapsamında değil" satırı vardı. **Artık doğru değil** — Gmail entegrasyonu (Sprint 9, `gmail.readonly` scope) kod olarak tamamlandı. Bkz. aşağıdaki envanterin 4. maddesi; bu artık N/A değil, avukata görülmesi gereken açık bir kalem. **Güncelleme (aynı gün):** madde 7'deki CASA/verification maliyeti ($15k-$75k, 4-12+ hafta) netleşince, o karar verilene kadar `EmailIntegrations:Enabled=false` flag'iyle kullanıcıdan tamamen gizlendi (`Matching:Enabled` ile aynı desen) — bkz. `DECISIONS.md` "Gmail Integration (Phase 9) — kullanıcıdan gizlendi (2026-08-29)".
>
> **Güncelleme (2026-08-31):** Yukarıdaki iki notta anlatılan Gmail OAuth entegrasyonu artık sadece
> gizli değil, **koddan tamamen kaldırıldı** — bürokratik (CASA) ve maddi maliyet nedeniyle bu
> yatırımın ne yakın ne orta vadede yapılmayacağı netleşti. Aşağıdaki envanterdeki "Gmail bağlantısı"
> satırı ve "Eksik" listesindeki madde 2/7 artık **N/A**; kalan tek email-tabanlı özellik, kullanıcının
> kendi filtresiyle yönlendirdiği maili işleyen Forwarding path'i (OAuth yok, `gmail.readonly` gibi bir
> scope yok — bkz. `DECISIONS.md`'nin ilgili girdisi).

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
| ~~CV / profil metni (`CandidateProfile.CvText`, `JobMatch.CvTextSnapshot`)~~ (2026-09-02'de koddan kaldırıldı, bkz. `DECISIONS.md`) | ~~Cloud SQL, düz metin (şifrelenmemiş)~~ | N/A |
| ~~Gmail bağlantısı~~ (2026-08-31'de koddan kaldırıldı, bkz. not yukarıda) | ~~OAuth token (encrypted) DB'de; `gmail.readonly` scope~~ | N/A |
| Email eşleştirme sonucu (`EmailSuggestion`) | Cloud SQL — `messageId`/`threadId`/`senderDomain`/`matchedRule`/`confidenceScore` **ve `Subject`/`Snippet` (max 500/2000 karakter) persist ediliyor** (`EmailSuggestionConfiguration`) — kullanıcının öneriyi incelerken görebilmesi için bilinçli bir tasarım, ama **düzeltme (2026-09-02): bu satırda daha önce "Subject/Snippet DB'ye yazılmıyor, sadece bellekte kullanılıp atılıyor" yazıyordu — bu yanlıştı**, kod böyle çalışmıyor. Sadece e-postanın tam gövdesi (body) hiç tutulmuyor. | **Evet — OpenAI API'ye** (sınıflandırma/extraction anında Subject/Snippet gönderiliyor, `OpenAiEmailClassificationProvider`/`OpenAiEmailJobExtractionProvider`, ABD merkezli, yurt dışı aktarım) — **artık disclosure edildi (2026-09-02)**, bkz. `/privacy#cross-border-transfer` |
| Personal Access Token (extension) | Sadece hash (`TokenHash`) DB'de, düz metin token bir daha gösterilmiyor | Hayır |
| Google ile giriş (2026-09-05, isteğe bağlı) — Google hesabının sabit kimliği (`sub`), doğrulanmış e-posta, ad/soyad | `AspNetUserLogins` (provider=`Google`, key=`sub`) + `AspNetUsers` (e-posta/ad/soyad, `EmailConfirmed=true`); Google access/refresh token'ı **saklanmıyor**, sadece ID token'dan kimlik okunuyor (`GoogleAuthClient`) | Giriş anında tarayıcı `accounts.google.com`'a yönleniyor ve sunucu kodu Google'ın token ucuna gönderiyor (scope: `openid email profile`, hassas olmayan; CASA/app verification gerekmiyor). Google'a bizden başka veri gitmiyor. `/privacy#google-sign-in` bölümünde açıklandı (2026-09-05) |
| Extension'ın scrape ettiği veri (LinkedIn/kariyer.net job title/company/location/description/URL) | Cloud SQL (Application/Job/Company) | Hayır |
| Gmail Taraması (2026-09-03, isteğe bağlı, varsayılan kapalı) — kullanıcının Gmail'de kendi açtığı bir e-postanın sender/subject/body'si, `EmailProvider.Extension` bağlantısı üzerinden | İlgililik skorlaması tamamen tarayıcıda (cihazda) yapılıyor — eşiği geçen bir mesajın yalnızca sender/subject/kısa snippet'i sunucuya ulaşıyor, eşiği geçmeyen hiçbir şey e-kariyerim'e hiç gönderilmiyor. Sunucuya ulaşan kısım, Mail Forwarding'deki `EmailSuggestion` satırıyla aynı şekilde Cloud SQL'de saklanıyor (Subject/Snippet, tam e-posta gövdesi asla). | Sunucuya ulaşan kısım için **Evet — OpenAI API'ye**, Mail Forwarding ile aynı pipeline (`ClassifyAsync`/`ProcessSignalAsync` artık her iki intake yolunu da paylaşıyor) — disclosure edildi (2026-09-03), bkz. `/privacy#cross-border-transfer` |
| Hata/performans telemetrisi | Sentry (backend `Sentry.AspNetCore`, frontend `@sentry/nextjs`) — konfigürasyon varsayılan (`sendDefaultPii` açıkça ayarlanmamış), hangi alanların gittiği doğrulanmadı | **Evet — Sentry'ye** (SaaS, muhtemelen ABD/AB veri merkezi seçimine bağlı, hesap ayarları kontrol edilmeli) |
| Tüm altyapı (Cloud Run ×2, Cloud SQL, Memorystore) | Google Cloud, `europe-west1` (AB bölgesi) | Google Cloud (alt-işlemci) |

### Eksik / avukata sorulması gereken kalemler

1. **Aydınlatma Metni (KVKK m.10 formatı)** — `/privacy` sayfası var ama informal; m.10'un istediği spesifik başlıklar (veri sorumlusu kimliği, işlenen veri kategorileri, işleme amaçları, aktarılan alıcı grupları, toplama yöntemi/hukuki sebebi, m.11 hakları) format olarak karşılanmıyor.
2. **Granüler açık rıza yok** — CV/OpenAI için 2026-09-01'de eklenen spesifik onay kutusu, özelliğin kendisiyle birlikte 2026-09-02'de kaldırıldı (bkz. `DECISIONS.md`) — bu madde tekrar açık. Gmail okuma izni kısmı zaten N/A (Gmail entegrasyonu koddan tamamen kaldırıldı, bkz. yukarıdaki not). **Hâlâ açık:** `EmailIntegrations`'ın OpenAI'a gönderdiği email subject/snippet için ayrı bir granüler rıza yok — kullanıcı sadece genel kayıt onayıyla (`ConsentAcceptedAt`) ve Mail Forwarding'i kendi isteğiyle kurarak bu özelliği kullanmış oluyor, CV/OpenAI'daki gibi spesifik bir onay kutusu yok. Bu bilinçli bir ürün kararı değil, sadece henüz eklenmedi — avukata danışılmalı (CV/OpenAI'daki gibi ayrı bir onay kutusu mu gerekiyor, yoksa disclosure + genel onay yeterli mi).
3. ~~**Yurt dışı aktarım disclosure'ı**~~ — CV/OpenAI kısmı için eklenen `/privacy#cross-border-transfer` bölümü, özellikle birlikte kaldırılmıştı. **Çözüldü (2026-09-02):** aynı bölüm bu kez `EmailIntegrations`'a özgü olarak yeniden eklendi — Subject/Snippet'in OpenAI'a (ABD) gönderildiği artık isimle disclosure ediliyor, bkz. "Yapıldı" bölümü. Sentry hâlâ isimlendirilmedi — KVKK'nın 2024 değişikliğiyle gelen yurt dışı aktarım rejimi (açık rıza veya yeterlilik kararı/SCC) hangisine dayanacağımız Sentry için hâlâ netleşmedi.
4. **ToS / Kullanım Şartları** — hiç yok.
5. **VERBİS kaydı** — muafiyet kapsamına girip girmediğimiz (ölçek/ana faaliyet kriterleri) teyit edilmemiş.
6. **Çerez Politikası** — yok. `NEXT_LOCALE` ve theme cookie'leri fonksiyonel/zorunlu görünüyor ama yine de disclosure gerekir.
7. ~~**Gmail API "restricted scope" platform uyumu**~~ — **N/A (2026-08-31):** Gmail OAuth entegrasyonu koddan tamamen kaldırıldı, `gmail.readonly` scope'u artık kullanılmıyor.
8. ~~**Özel nitelikli veri riski (CV serbest metni)**~~ — **N/A (2026-09-02):** CV'yi serbest metin olarak alan özellik (AI Job Matching) tamamen kaldırıldı, bu risk artık yok.
9. **Consent versioning** — zaten yukarıda backlog'da not düşülmüş, hâlâ açık. (CV/OpenAI rızası kendi kapsamında bunu hafifletiyordu, ama o özellik 2026-09-02'de kaldırıldığı için bu kısmi mitigasyon da artık yok.) Genel `ConsentAcceptedAt` (kayıt onayı) için versioning hâlâ yok.

### Olumlu / avukatın işini kolaylaştıran noktalar

- E-postanın tam gövdesi (body) hiç persist edilmiyor — sadece subject/snippet ve metadata tutuluyor (bkz. yukarıdaki düzeltme notu).
- PAT'lar hash'li saklanıyor, düz metin yok.
- Hesap silme + veri export zaten uçtan uca çalışıyor ve test edilmiş.
- Tüm altyapı AB bölgesinde (`europe-west1`).
- Rate limiting ve zip-bomb koruması mevcut.
