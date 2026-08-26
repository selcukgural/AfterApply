# Product & Technical Decisions

Spec kuralı §31.18 gereği: belirsiz kararlar varsayım yapılmadan burada
önerilir ve kullanıcı onayına bırakılır. Bu dosya `afterapply-intelligence-platform-plan.md`
§35'teki "Hâlâ Açık" listesini takip eder.

Durum etiketleri: `DECIDED` (spec'te zaten karara bağlanmış),
`PROPOSED` (öneri var, kullanıcı onayı bekleniyor), `OPEN` (henüz öneri yok).

---

## Zaten karara bağlı (spec §35)

- **Market:** Türkiye-first, global-ready — DECIDED
- **Architecture:** Modular Monolith + Clean Architecture, mikroservis yok — DECIDED
- **MVP positioning:** Job Application Tracker + Personal Analytics — DECIDED
- **North star data asset:** Verified Application Outcomes — DECIDED

---

## 0. Ürün/Solution adı — DECIDED

**AfterApply.** Repo ve `.slnx` adıyla devam edilecek
(`AfterApply.Api`, `AfterApply.Application`, `AfterApply.Domain`,
`AfterApply.Infrastructure`). Spec'teki `JobTracker.*` referansları isim
örneği olarak kabul edilir, kod bunlarla değil `AfterApply.*` ile yazılır.

## 1. Modüler monolith solution yapısı — DECIDED

**Layer-first.** 4 proje (`Api`, `Application`, `Domain`, `Infrastructure`),
modüller (Identity, Applications, Companies, Jobs, Imports, Analytics,
Notifications) her katmanın içinde namespace/klasör olarak ayrılır
(`Domain/Applications`, `Domain/Companies`, ...). NetArchTest ile modüller
arası yanlış bağımlılık (örn. Companies'in Applications'a domain
seviyesinde bağımlı olması) test edilecek.

## 2. Authentication — DECIDED

**ASP.NET Core Identity + JWT (self-hosted).** Vendor bağımlılığı/maliyeti
yok, kullanıcı verisi tamamen kendi altyapımızda kalır (Privacy First
ilkesiyle uyumlu, §2).

## 3. Frontend framework — DECIDED

**Next.js (React), App Router, TypeScript, Tailwind CSS 4.** Blazor
alternatifine karşı doğrudan sorulup Next.js/React seçildi (Sprint 2).
`web/` klasöründe, `AfterApply.slnx`'in dışında, ayrı bir deployable.

## 4. Background jobs — DECIDED

**Hangfire.** PostgreSQL-backed persistence, built-in retry/dashboard —
spec §31.12'deki "retry-safe" gereksinimini kutudan çıktığı gibi karşılıyor.

## 5. Test assertion kütüphanesi — DECIDED

**Shouldly (MIT).** FluentAssertions 8+ ticari lisansa geçti (belirli gelir
eşiğinin üzerindeki şirketler için ücretli); AfterApply'ın monetizasyon
planları (§18) nedeniyle bu ileride sorun olabilirdi. Shouldly, FluentAssertions'a
çok yakın syntax sağlıyor ve lisans sorusunu tamamen ortadan kaldırıyor.

## Sprint 0 sırasında keşfedilen: local port çakışmaları — bilgi amaçlı

Geliştirme makinesinde başka bir yerel projenin (podman) container'ları
5433 (Postgres) ve 6379 (Redis) portlarını zaten kullanıyordu; native
Homebrew Postgres 5432'yi kullanıyor ama Redis brew servisi hiç
çalışmıyordu (6379'a "native Redis" sanılan şey aslında diğer projenin
container'ıydı). Bu proje-özel bir bulgu, ürün kararı değil — ancak
`docker-compose.yml`'de host portları buna göre offsetlendi (Postgres
5434, Redis 6382) ve README'ye port çakışması kontrolü notu eklendi.

## 5. Cloud provider — DECIDED (2026-08-26, Sprint 13 planlaması)

Azure/AWS/kendi VPS'i gibi ücretli seçenekler yerine, kullanıcının tercihiyle
kalıcı gerçek ücretsiz katmanlı, kanıtlanmış bir stack seçildi (deneme kredisi
değil — Railway/Fly.io gibi ücretsiz tier'ını sonradan kaldırmış servislerden
bilinçli olarak kaçınıldı):

- **Frontend (Next.js):** Vercel free tier — otomatik ücretsiz SSL, custom
  domain destekli.
- **Backend (.NET API, container):** Google Cloud Run free tier — aylık
  ~2M istek + cömert CPU/RAM-saniye kotası, mevcut
  `src/AfterApply.Api/Dockerfile` doğrudan kullanılır, custom domain'de
  otomatik ücretsiz SSL (DEPLOYMENT.md'nin "no reverse proxy/TLS" notunu
  kapatıyor), trafik yokken sıfıra iner (cold start — henüz gerçek
  kullanıcı yokken kabul edilebilir bir tradeoff).
- **Postgres:** Neon free tier — kalıcı ücretsiz katman (3GB, branching,
  kullanılmayınca otomatik askıya alma/uyanma), standart Postgres
  wire-protokolü olduğu için EF Core'un mevcut connection string modeli
  değişmeden çalışır.
- **Secrets:** Cloud Run'ın entegre Secret Manager'ı (ücretsiz tier) —
  DEPLOYMENT.md'nin "no secrets manager" notunu kapatıyor.

**Bilinen tradeoff:** free tier'ların cold start/otomatik-askıya-alma
davranışı gerçek trafik/launch anında yeniden değerlendirilmesi gereken bir
sınır — bu, "gerçek launch yaklaştığında paid'e geçilebilir" şeklinde bilinçli
bırakıldı, MVP öncesi paid altyapıya yatırım yapmamak tercih edildi.

### Redis — DECIDED (2026-08-26)

Sprint 13 planlaması sırasında bulgu: kod tabanında Redis şu an hiçbir iş
mantığı tarafından kullanılmıyor — `RateLimiting.cs`'teki policy'ler
`RateLimitPartition.GetFixedWindowLimiter` ile in-memory çalışıyor, sadece
`AddHealthChecks().AddRedis(...)` Redis'e bağlı (`DependencyInjection.cs`).
Yani spec'in "Redis where justified" notu bugüne kadar hiç tetiklenmemiş.
Buna rağmen kullanıcı Upstash free tier eklenmesine karar verdi — ileride
cache/distributed rate-limiting ihtiyacı çıkarsa altyapı hazır olsun diye
(YAGNI'yi ihlal eden bir seçim değil: Upstash da ücretsiz, aktif bir maliyet
yaratmıyor).

### Error tracking / observability — DECIDED (2026-08-26)

Sentry — hem .NET (backend) hem Next.js (frontend) SDK'ları var, ücretsiz
tier bu ölçekteki bir MVP için yeterli.

## 6-12. Diğer açık kararlar (spec §35)

Şu an bloklayıcı değil, ilgili sprint'te netleştirilecek:

- Exact free-tier limits / Pro pricing → Monetization henüz MVP kapsamında değil
- Email integration provider priority → Sprint 9 (post-MVP)
- AI provider and cost strategy → Phase 11 (post-MVP)
- Public company analytics minimum sample thresholds → Phase 10, spec §15'teki
  hipotez (`<20 Hidden, 20-49 Very Low, ...`) başlangıç noktası olarak kullanılabilir
- Company identity resolution strategy → Sprint 1'de basit normalization
  (case/whitespace/suffix normalize) yeterli; fuzzy matching post-MVP
- Browser extension release timing → Phase 12, veri hacmi ve LinkedIn
  import adoption'ına göre
- Product name / brand → "AfterApply" öneri #0 ile aynı karara bağlı

---

## Sprint 1 kararları ve bulguları

### Entity Id — DECIDED

`Guid.CreateVersion7()`, entity constructor'ında üretiliyor (DB default değil)
— ileride offline/import-time id üretimi (Sprint 4/5) için önemli.

### Domain, User'ı modellemez — DECIDED

ASP.NET Core Identity'nin `ApplicationUser : IdentityUser<Guid>`'ı kimlik
verisinin tek kaynağı; Domain/Application hiç referans almıyor, her entity
sahiplik için düz bir `Guid UserId` taşıyor. Sebep: `IdentityUser<TKey>`
`Microsoft.AspNetCore.Identity` namespace'inde, `LayerDependencyTests`'in
Domain kuralını ihlal eder; ayrıca bu sprint'te User üzerinde gerçek bir
domain davranışı yok (register/login/profile zaten `UserManager`/
`SignInManager`'ın işi) — paralel bir `Domain.Identity.User` aggregate'i
gereksiz ceremony olurdu (YAGNI).

`AppDbContext` → `IdentityUserContext<ApplicationUser, Guid>` (roller
gereksiz, `IdentityDbContext` değil).

### Kullanılmayan pattern: MediatR — DECIDED

Kullanılmıyor. Use-case orchestration düz interface-backed servis sınıfları
ile yapılıyor (`IApplicationService`/`ApplicationService`, `IAuthService`/
`AuthService` vb.) — interface Application katmanında, implementasyon
Infrastructure'da. Sprint 2+ bu pattern'i takip etmeli.

### ApplicationEventType spec'ten sapıyor — DECIDED

Spec §280-292'nin örnek event listesi hem `Rejected`/`Withdrawn`'ı event tipi
hem de `ApplicationStatus` değeri olarak veriyor — bu gerçek bir redundancy
(spec kendi listesini "örnek" olarak işaretliyor, bağlayıcı değil). Final
enum bu ikisini `StatusChanged`'a indirgiyor, 9 değer: ApplicationCreated,
ApplicationSubmitted, RecruiterContacted, ScreeningStarted,
InterviewScheduled, InterviewCompleted, OfferReceived, FollowUpSent,
StatusChanged.

### Company/Job normalization sınırı — DECIDED (bilinen sınırlama)

`CompanyNameNormalizer`/`Job.NormalizeTitle` case/whitespace/suffix
normalize ediyor. Bunun ötesinde: .NET invariant culture Türkçe noktalı/
noktasız I (İ/I/ı/i) çiftini round-trip etmiyor (`ToUpperInvariant('ı')`
'ı' olarak kalıyor, 'I'ya dönmüyor) — bu, Türkiye-first bir üründe
"Yazılım" ile "YAZILIM"ın farklı normalize edilmesi gibi gerçek bir
dedup bugı yaratıyordu. `Common/TurkishTextNormalizer.FoldCase` ile
düzeltildi (test: `CompanyNameNormalizerTests.Normalize_Folds_Turkish_
Dotted_And_Dotless_I_Together`). Hâlâ **yapmadığı**: "ABC Teknoloji" ile
"ABC Tech" gibi textual synonym'leri birleştirmiyor — bu fuzzy matching,
post-MVP.

### Paket versiyonları 10.0.11'e hizalandı — DECIDED

Sprint 0'ın `Microsoft.EntityFrameworkCore.Design`'ı 10.0.4'e indirip
Npgsql'in floor'una eşleme "workaround"u yerine, tüm
`Microsoft.AspNetCore.*`/`Microsoft.EntityFrameworkCore*` ailesi tek ve
güncel patch'e (10.0.11) hizalandı; `Microsoft.EntityFrameworkCore`
Infrastructure'a explicit paket referansı olarak eklendi (NuGet'in en
düşük tatmin eden versiyona düşmesini önlemek için). `Microsoft.OpenApi`
2.12.2'de bırakıldı (3.x'e geçiş OpenApi source generator'ını kırıyor,
Sprint 0'da bulundu).

### EF Core: sibling collection navigation + SaveChanges bug'ı — bilgi amaçlı

`Application.Events`/`StatusHistory` gibi iki kardeş collection navigation'ı
`.Include()` ile yükleyip sonra domain metoduyla yeni item eklemek
(`_events.Add(...)`), EF Core'un change tracker'ının yeni item'ı `Added`
yerine `Modified` (UPDATE) olarak işaretlemesine yol açtı — INSERT yerine
var olmayan bir `Id`'ye UPDATE denendiği için `DbUpdateConcurrencyException`
(0 rows affected) fırlatıyordu. `AsSplitQuery()` bunu çözmedi. Gerçek çözüm:
`ChangeStatusAsync`/`AddEventAsync`, aggregate'i Include'sız yüklüyor, domain
metodunu çağırdıktan sonra yeni child entity'yi `dbContext.ApplicationEvents
.Add(...)` ile **açıkça** DbSet'e ekliyor — EF'in Include-tabanlı collection
tracking belirsizliğine hiç girmiyor. Bu Sprint 2+'ta benzer "tracked
aggregate + yeni child ekleme" senaryolarında hatırlanmalı.

### dotnet-ef tooling: stray `bin\Debug` artifact — bilgi amaçlı

Global `dotnet-ef` tool (10.0.3) runtime'dan (10.0.11) eski olduğu için
`dotnet ef migrations add`/`database update` çalışırken (muhtemelen
cross-platform path handling bug'ı yüzünden) `bin/` içine literal
backslash'li bir `bin\Debug` klasörü yazdı — bu, `**/*.resx` glob hatası
ve "nested bin" copy retry uyarılarına yol açan ve sonraki temiz build'leri
bozan bir artifact'tı. Temizlendi (`rm -rf "bin\\Debug"`). Sprint 2+'ta yeni
migration eklerken bu tekrar olursa aynı şekilde temizlenmeli; kalıcı çözüm
`dotnet tool update -g dotnet-ef` ile global tool'u güncellemek olabilir
(bu oturumda yapılmadı — proje dosyalarını etkileyen bir değişiklik değil).

---

## Sprint 2 kararları ve bulguları

### Token storage: localStorage + single-flight refresh — DECIDED

Access+refresh token `localStorage`'da, access token ayrıca modül-seviyesi
bir singleton'da (senkron okuma için). Cookie/BFF yok. 401 alındığında
tek-uçuşlu (single-flight) bir refresh mekanizması aynı anda birden fazla
refresh çağrısının backend'in "reuse edilmiş refresh token → tüm tokenları
iptal et" davranışını tetiklemesini önlüyor (bkz. `AuthService.RefreshAsync`).
Cross-tab senkronizasyon yok (her tab kendi modül state'ine sahip) — bilinen
sınırlama, Sprint 7'de gözden geçirilebilir.

### Backend pagination/filter/sort + `/summary` endpoint — DECIDED

`GET /api/applications` artık `page`/`pageSize`/`search`/`status`/`sortBy`/
`sortDirection` query param'ları alıyor, `PagedResult<T>` dönüyor. Yeni
`GET /api/applications/summary` endpoint'i dashboard sayaçları için ayrı
bir aggregation sorgusu çalıştırıyor (liste artık sayfalı olduğu için
client-side toplam hesaplanamıyor).

### Dashboard durum-bucket eşlemesi — DECIDED

Applied+Screening → Aktif; Interview+TechnicalInterview+FinalInterview →
Aktif + Mülakatlar; Offer → Bekleyen + Teklifler; Accepted/Withdrawn →
sadece Toplam'da (ayrı tile yok, spec §294-304 listelemiyor); Rejected ve
Ghosted ayrı tile'lar (Ghosted, Rejected'dan kasıtlı olarak ayrı tutuluyor —
ürünün ghosting-detection değer önerisinin merkezinde farklı bir sinyal).

### CORS — DECIDED

Config-driven (`Cors:AllowedOrigins`), kod içinde hardcoded origin yok,
sadece `appsettings.Development.json`'da `http://localhost:3000`.
`AllowCredentials()` kullanılmıyor — Bearer token modeli cookie
gerektirmiyor, YAGNI.

### TypeScript versiyonu — bilgi amaçlı

`create-next-app@16.3.2`'nin kendi template'i `"typescript": "^5"` pin'liyor
(5.9.3 kuruldu), TS7 (yeni Go-tabanlı derleyici, bu oturumda `npm view`
ile `latest` olduğu doğrulandı) DEĞİL — plandaki "6.0.3'e pinle" önlemi
gereksiz çıktı, template zaten güvenli bir 5.x sürümünü kullanıyor.

### Gerçek bug'lar: enum JSON serialization + LINQ query — düzeltildi

1. Enum'lar varsayılan olarak sayı olarak serialize/deserialize ediliyordu
   — `ConfigureHttpJsonOptions` ile `JsonStringEnumConverter` eklendi
   (`Program.cs`).
2. `ApplicationService.GetAllAsync` — `.Join()` sonrası `.OrderByDescending`
   projection'dan SONRA yapılıyordu, EF Core SQL'e çeviremiyordu; sıralama
   join'den hemen sonra, projection'dan ÖNCE taşındı.

### Test ortamı bulgusu: port tutarlılığı — bilgi amaçlı

Bu oturumda backend'i uzun süre `--urls http://localhost:5299` ile (README/
`launchSettings.json`'ın resmi portu 5151 yerine) çalıştırmışım; frontend'in
`.env.local`'ı 5151'i bekliyordu, bu da ilk tarayıcı testinde sessiz bir
"Kayıt oluşturulamadı" hatasına yol açtı (bağlantı reddedildi, CORS hatası
değil). Backend'i `--launch-profile http` ile (resmi 5151 portu) yeniden
başlatarak çözüldü. Ders: yerel geliştirmede her zaman `launchSettings.json`
profilini kullan, ad-hoc `--urls` override'larından kaçın.

### Tarayıcı ortamı bulgusu: eski service worker — bilgi amaçlı

Bu makinenin Chrome profilinde, port 3000'de daha önce çalışmış tamamen
alakasız bir projeden ("Aethermoor Chronicles") kalma bir service worker +
cache vardı; `localhost:3000`'e ilk navigasyonda AfterApply yerine o eski
uygulamayı cache'ten servis etti. `navigator.serviceWorker.getRegistrations()`
+ `caches.delete()` ile temizlendi. Proje koduyla ilgisi yok, paylaşılan
tarayıcı profilinin geçmişinden kaynaklanan bir ortam sorunu.

### CSS bug: dark-mode arkaplanı Tailwind class'ını eziyordu — düzeltildi

`create-next-app` scaffold'unun `globals.css`'i `body { background: var(--background) }`
kuralı içeriyordu, `--background` `prefers-color-scheme: dark` altında
`#0a0a0a` oluyordu — bu, aynı `body` elementindeki `bg-gray-50` Tailwind
class'ını eziyordu (OS dark mode'daysa sayfa siyah görünüyordu). Kullanılmayan
scaffold CSS'i temizlendi, `min-h-full` → `min-h-screen` yapıldı (viewport
kapsamını percentage-height zincirine değil doğrudan garanti eder).

---

## Sprint 3 kararları ve bulguları

### "Yanıt aldı" tanımı — DECIDED

Bir başvuru "yanıt aldı" sayılır ⇔ `ApplicationStatusHistory`'de
`ToStatus ∈ {Screening, Interview, TechnicalInterview, FinalInterview,
Offer, Rejected, Accepted}` olan en az bir kayıt varsa. Bilinçli olarak
`Withdrawn`'ı dışarıda bırakıyor (aday-kaynaklı, işveren sinyali değil) ve
`Ghosted`'ı da dışarıda bırakıyor (spec §5 ghosting'i açıkça "yanıt yok"
olarak tanımlıyor). Bu, current-status yerine **history-tabanlı** ("hiç
ulaştı mı") bir tanım — `Applied→Screening→Ghosted` gibi bir başvuru, güncel
durumu `Ghosted` olsa bile doğru şekilde "yanıt aldı" sayılıyor.

Mülakat Oranı / Teklif Oranı da aynı history-tabanlı mantığı kullanıyor
(sırasıyla `{Interview,TechnicalInterview,FinalInterview}` /
`{Offer,Accepted}`'a hiç ulaştı mı) — güncel durum yerine, çünkü mülakat
sonrası reddedilen biri hâlâ Mülakat Oranı'na girmeli.

**Red Oranı / Kayboldu Oranı ise *güncel* `Status`'u kullanıyor** —
Sprint 2'nin `GetSummaryCountsAsync` dashboard tile mantığıyla tutarlı
kalması için (aynı sayfada üstteki tile'larla çelişmesin diye).

### Yanıt süresi — DECIDED

`ChangedAt − AppliedAt`, "yanıt aldı" kümesine (yukarıdaki) uyan İLK
history kaydı üzerinden. Hiç yanıt almamış başvurular (hâlâ `Applied`,
veya sadece `Withdrawn`/`Ghosted`'a geçmiş) ortalama/medyandan tamamen
hariç tutuluyor — 0 olarak sayılmıyor.

### Tek endpoint: `GET /api/analytics/overview` — DECIDED

Spec §21'in önerdiği 3 endpoint (`/overview`, `/response-times`,
`/status-distribution`) yerine tek endpoint — üçü de aynı iki sorgudan
besleniyor, ayırmak tek bir dashboard bölümü için 3 kat DB round-trip
demek olurdu.

### Medyan C#'ta hesaplanıyor — DECIDED

Postgres'in `percentile_cont`'u yerine, kullanıcı-başına veri hacmi
(response time listesi) bellekte medyan hesaplamak için yeterince küçük
olduğundan, EF Core/LINQ'un native medyan çevirisi olmadığından, ve bu
kod tabanında henüz raw-SQL pattern'i gerekmediğinden. Saf fonksiyon
(`AnalyticsCalculations`, DB bağımlılığı yok) unit test'lerle doğrulandı.

### Grafik: Recharts BarChart — DECIDED

`recharts@3.10.1` (React 19 uyumlu, npm'den canlı doğrulandı). 10 sıralı
pipeline aşaması pasta dilimlerinden çok soldan-sağa bar chart'ta daha
okunaklı. Yeni `/analytics` route'u yok — mevcut dashboard sayfasına
("Kişisel Analitik" bölümü) entegre edildi.

Uçtan uca tarayıcı testinde entegrasyon testindeki hesaplamalarla birebir
eşleşen sonuçlar gözlemlendi (bkz. `AnalyticsOverviewTests.cs`).

---

## Sprint 4 kararları ve bulguları

### CSV parser: CsvHelper — DECIDED

.NET'te de-facto standart, dual MS-PL/Apache lisans (FluentAssertions'daki
gibi ticari lisans riski yok — bkz. Sprint 0/1 test kütüphanesi kararı).

### Column mapping: auto-detect + opsiyonel override — DECIDED

Generic CSV farklı kullanıcılardan farklı başlıklarla gelebileceği için sabit
sütun sırası varsayılmıyor. `CsvColumnMapper` (saf fonksiyon,
`AnalyticsCalculations` paterni) bilinen TR/EN alias tablosuyla otomatik
eşleme yapıyor (Company/Şirket, Title/Pozisyon, Applied At/Tarih zorunlu;
Status/Durum, Job URL/Link, Location/Konum opsiyonel). Auto-detect zorunlu
alanlardan birini bulamazsa, `POST /api/imports/csv` isteğindeki opsiyonel
`columnMapping` form alanıyla (JSON, field adı → header adı) override
edilebilir.

### Dedup key'leri generic CSV'ye uyarlandı — DECIDED

Spec §8'in sırası (Source+ExternalId → Job URL → Company+JobTitle+AppliedAt
→ fuzzy) generic CSV'de birebir uygulanamıyor: stabil bir external id sütunu
varsayılamaz, bu yüzden Source+ExternalId adımı atlanıyor. Uygulanan sıra:
(1) `JobUrl` tam eşleşmesi (satırda varsa, kullanıcının mevcut
application'larına karşı), (2) normalize edilmiş Company (`CompanyId` via
mevcut `ICompanyResolver`) + normalize edilmiş JobTitle
(`JobTitleNormalizer`, aşağıya bkz.) + `AppliedAt` (gün hassasiyeti)
eşleşmesi. Bu iki set (URL'ler + company/title/date üçlüleri), import
başında kullanıcının mevcut kayıtlarından bir kez yükleniyor ve yeni
eklenen her satırla güncelleniyor — hem DB'deki mevcut kayıtlarla hem de
**aynı CSV içindeki** tekrarlarla dedup sağlıyor (idempotency DoD'si:
aynı dosya iki kez yüklendiğinde ikinci seferde 0 yeni kayıt — manuel ve
integration testle doğrulandı). Fuzzy matching zaten post-MVP kararlı.

### `JobTitleNormalizer` çıkarıldı — DECIDED

`Job.NormalizeTitle` private static metodu, `CompanyNameNormalizer`'a
paralel bir `Domain.Jobs.JobTitleNormalizer` public static sınıfına
taşındı — hem `Job.Create` hem de import dedup'ının aynı normalizasyonu
(Turkish-aware fold + whitespace collapse) kullanması gerekiyordu.

### İçe aktarılan kayıtlarda `EmploymentType` — bilinen sınırlama

`Application.Create` zorunlu bir `EmploymentType` alıyor ama generic CSV
import'u bu sütunu map etmiyor (plan kapsamında yalnızca CompanyName/
JobTitle/AppliedAt zorunlu, Status/JobUrl/Location opsiyonel tutuldu).
İçe aktarılan tüm kayıtlar `EmploymentType.FullTime` ile oluşturuluyor.
İleride bir EmploymentType alias sütunu eklenmesi gerekirse
`CsvColumnMapper`/`ImportRowParser`'a yeni bir opsiyonel alan olarak
eklenebilir.

### İşleme senkron — DECIDED

Hangfire henüz yok (Sprint 6). `Imports:MaxFileSizeBytes` (varsayılan 5 MB)
ve `Imports:MaxRowCount` (varsayılan 5000) config'leri (appsettings'ten,
hard-code değil) senkron işlemeyi güvenli kılıyor; aşılırsa
`CsvImportValidationException` ile 400 dönüyor.

### Bug: `IFormFile` bağlayan minimal API endpoint'i antiforgery ister — düzeltildi

.NET 8+'ta minimal API'de `[FromForm]`/`IFormFile` bağlayan bir endpoint,
antiforgery servisleri hiç register edilmemiş olsa bile varsayılan olarak
antiforgery metadata'sı taşıyor; bu proje antiforgery/cookie kullanmadığı
(Bearer JWT + `AllowCredentials()` yok — bkz. Sprint 2 CORS kararı) için
`POST /api/imports/csv` her istekte 500 (unhandled exception, "no
middleware found") atıyordu. `.DisableAntiforgery()` endpoint'e eklenerek
düzeltildi — CSRF zaten bu API'de anlamsız, cookie-based auth yok.

### dotnet-ef `bin\Debug` artifact'ı tekrar oluştu — bilgi amaçlı

Sprint 1'de belgelenen aynı stray-artifact bug'ı (`dotnet ef migrations
add`, global `dotnet-ef` 10.0.3'ün runtime 10.0.11'den eski olması
yüzünden) bu sprintte de oluştu; aynı şekilde temizlendi
(`rm -rf "bin\\Debug"`). Kalıcı çözüm hâlâ yapılmadı (bkz. Sprint 1 notu).

---

## Sprint 5 kararları ve bulguları

### ZIP işleme: `System.IO.Compression.ZipArchive` — DECIDED

BCL'in kendi API'si, yeni NuGet paketi gerekmiyor. Sadece adı
`Job Applications(_N)?.csv` desenine uyan entry'lerin stream'i açılıyor;
eşleşmeyen entry'ler (tam LinkedIn export'unda `Messages.csv`,
`Connections.csv` vb. onlarca dosya) sadece metadata seviyesinde enumerate
ediliyor, decompress edilmiyor. Dosya sistemine extract edilmediği (stream
doğrudan `CsvReader`'a veriliyor) için "zip slip" (path traversal) bu
tasarımda uygulanabilir değil — ayrı bir kontrol eklenmedi.

### Baseline ZIP limitleri, Sprint 7'nin tam hardening'inin yerine değil öncesinde — DECIDED

`Imports:MaxZipSizeBytes` (varsayılan 50 MB), `Imports:MaxZipEntryCount`
(varsayılan 500); eşleşen her CSV entry'si için `entry.Length` (uncompressed,
metadata'dan, stream açmadan) mevcut `Imports:MaxFileSizeBytes`'a karşı
kontrol ediliyor. `Imports:MaxRowCount` artık ZIP'teki tüm eşleşen
dosyaların toplamına uygulanıyor. Kapsamlı rate limiting / zip-bomb
testleri hâlâ Sprint 7 kapsamında (DEVELOPMENT_PLAN.md).

### `Source.LinkedInImport` kullanıldı (spec'in `Source = LinkedIn` örneği değil) — DECIDED

Enum'da (`Domain/Common/Source.cs`) hem `LinkedIn` hem `LinkedInImport`
var; `LinkedIn` muhtemelen Phase 12 browser extension için ayrılmış. Bu
sprint'in Data Export import pipeline'ı `LinkedInImport`'u kullanıyor —
kodun kendi sözlüğüyle spec'in gevşek örnek metninden daha tutarlı.

### `Job` global resolution — `IJobResolver`/`JobResolver` — DECIDED

`ICompanyResolver`/`CompanyResolver` paterni (`Applications/CompanyResolver.cs`)
tekrarlandı: `(Source, ExternalId)` ile find-or-create. Bunu DB seviyesinde
zaten destekleyen bir unique index Sprint 1'den beri mevcuttu ama hiç
kullanılmamıştı — `JobConfiguration.cs`:
`HasIndex(j => new { j.Source, j.ExternalId }).IsUnique().HasFilter(...)`.
`Company` gibi `Job` da kullanıcılar arası paylaşılan referans veri olarak
resolve ediliyor (aynı LinkedIn ilanını farklı kullanıcılar import ederse
aynı `Job` satırına işaret eder); dedup KONTROLÜ yine kullanıcıya özel
kalıyor. `ExternalId` çıkarılamayan satırlarda Job yine de (dedup'suz)
oluşturuluyor — Application seviyesindeki JobUrl/Company+Title+AppliedAt
tier'ları zaten güvenlik ağı.

### LinkedIn Job ID URL'den çıkarılıyor, ayrı bir sütun değil — DECIDED

`LinkedInJobIdExtractor` (saf fonksiyon), `.../jobs/view/<id>` deseninden
regex ile sayısal ID çıkarıyor. Spec §7'nin örneği
(`Source = LinkedIn, ExternalId = 4449445627`) ve §8'in "LinkedIn Job ID
extraction" adım isimlendirmesi bunun bir CSV sütunu değil, URL'den türetilen
bir değer olduğunu gösteriyor.

### Dedup tier'ları — tier-0 eklendi, `ImportService` satır-işleme mantığı reuse edildi — DECIDED

Spec §8 sırası artık tam uygulanıyor: (0) `Source+ExternalId` (yalnızca
LinkedIn path'inde — CSV path'i hiçbir zaman `externalId` üretmediği için
bu tier CSV import'ta her zaman no-op, Sprint 4 davranışı değişmedi), (1)
`JobUrl` tam eşleşmesi, (2) Company+JobTitle+AppliedAt. `ImportCsvAsync`
ve yeni `ImportLinkedInZipAsync`, satır-başına parse/dedup/create mantığını
ortak `ProcessRowAsync`/`ProcessCsvAsync` private helper'larından reuse
ediyor (`ImportService.cs`) — Sprint 4'ün mevcut testleri (regresyon
kontrolü) değişmeden geçti.

### Tek `ImportBatch` / ZIP, `ImportBatch.Source` eklendi — DECIDED

Spec'in örnek çıktısı (`Total records: 1136, New: 1020, ...`) tek bir özet;
ZIP içindeki birden fazla `Job Applications_N.csv` toplanarak tek
`ImportBatch`'e yazılıyor. `ImportBatch`'e küçük, geriye uyumlu bir
`Source` alanı eklendi (yeni migration `AddImportBatchSource`) — `GET
/api/imports/{id}` artık CSV/LinkedIn ayrımını dönüyor.

### `Application.Create`'e opsiyonel `jobId` parametresi eklendi — DECIDED

`Application.JobId` Sprint 1'den beri vardı ama hiçbir kod yolu set
etmiyordu. Yeni bir "AssignJob" mutasyon metodu yerine, `Create` factory
metoduna trailing optional `Guid? jobId = null` parametresi eklendi (mevcut
tüm positional call site'lar — `ApplicationService.CreateAsync`, Sprint 4
CSV import path'i, `ApplicationTests.cs` — değişmeden derlendi) — aggregate
tek bir factory çağrısıyla tam kurulmuş oluyor, ayrı bir setter'ın
invariant riski taşımıyor.

---

## Sprint 6 kararları ve bulguları

### Yeni `Notifications` modülü ve kalıcı `Reminder` entity — DECIDED

Follow-up/ghosting önerileri salt on-demand hesaplama yerine kalıcı
`Reminder` entity (`src/AfterApply.Domain/Notifications/`) olarak
tutuluyor. Gerekçe: (1) Sprint 6'nın teslim kalemi Hangfire'a gerçek,
retry'lanabilir bir iş vermek — salt okunur bir hesaplama bunu
sağlamazdı; (2) DoD'deki "öneri düşer" ifadesi dismiss edilebilir bir
durum ima ediyor. Idempotency `(ApplicationId, Type, ReferenceAt)` unique
index'i ile sağlanıyor — `ReferenceAt` sadece gerçek bir statü
değişikliğinde ilerlediği için ayrı bir "N gün sorma" cooldown mantığı
gerekmedi.

### Staleness referans tarihi: `AppliedAt`'a düşen, seed-satırı hariç tutan hesap — DECIDED

`Application.Create`, `FromStatus == null` olan bir seed
`ApplicationStatusHistory` satırı ekliyor (bkz. Sprint 1). Bu satır
staleness hesabından hariç tutuluyor — yoksa geçmişe dönük (CSV/LinkedIn
import) bir başvuru yapay olarak taze görünürdü.
`ReminderCalculations.GetReferenceAt` = en son gerçek statü geçişinin
`ChangedAt`'ı, yoksa `AppliedAt`.

### Ürün kararları (kullanıcı ile netleştirildi, plan onayı sırasında)

- FollowUp herhangi bir terminal-olmayan durgun başvuruya uygulanır
  (sadece yanıtsızlarla sınırlı değil — örn. mülakat sonrası yanıt
  bekleyen bir başvuru da "takip et" önerisi alabilir).
- Bir başvuru hem FollowUp hem PossiblyGhosted koşulunu sağlarsa, sadece
  PossiblyGhosted gösterilir (daha güçlü sinyal önceliklidir).
- Dismiss, `ApplicationEventType.FollowUpSent`'i otomatik eklemez — bu
  event hâlâ tamamen kullanılmamış durumda, mevcut genel
  `POST /api/applications/{id}/events` ile ayrıca tetiklenebilir.
- `FollowUpThresholdDays` varsayılanı 7, `GhostingThresholdDays`
  varsayılanı 30 (spec'te zaten sabit).

### Cross-user query — bilinçli, izole bir istisna — DECIDED

`IReminderService.ScanAndGenerateRemindersAsync`'in `userId` parametresi
yok — Hangfire recurring job'ı tarafından çağrılıyor, `ClaimsPrincipal`
context'i yok. Bu, kod tabanındaki **tek** cross-user (tüm kullanıcılar
için tarama yapan) servis metodu; her diğer servis
`ClaimsPrincipal.GetUserId()` ile tek-kullanıcı scope'lu kalmaya devam
ediyor. Yeni bir servis eklerken bu istisnayı emsal olarak kullanmayın —
yalnızca background job'lar için geçerli.

### Hangfire şeması EF Core migration'larından ayrı — DECIDED

`Hangfire.PostgreSql`, `hangfire`-prefixli kendi şemasını runtime'da
otomatik oluşturuyor. `AppDbContext`'e eklenmedi, `AddReminders`
migration'ı yalnızca `Reminders` tablosunu içeriyor. Bu bilinçli bir
ayrım — ileride "eksik migration" sanılmasın diye not düşülüyor.

### Hangfire dashboard (`/hangfire`) bu sprintte eklenmedi — DECIDED

`ApplicationUser`/Identity'de rol kavramı yok (`AddIdentityCore`,
rolsüz). Dashboard'u açmak ya auth'suz bir ops yüzeyi ya da orantısız
yeni auth işi demek olurdu. `AddHangfire`/`AddHangfireServer()` tek
başına tüm retry/scheduling davranışını sağlıyor; dashboard +
`IDashboardAuthorizationFilter` Sprint 7'ye (Hardening) bırakıldı.

### Bulgu: statik `RecurringJob` facade'i `JobStorage.Current` olmadan çalışmıyor

`Program.cs`'te ilk denemede `RecurringJob.AddOrUpdate<T>(...)` (statik
facade) kullanıldı — entegrasyon testlerinde
`InvalidOperationException: Current JobStorage instance has not been
initialized yet` ile patladı. Sebep: modern `services.AddHangfire(...)`
DI kaydı, storage'ı yalnızca DI container'a bağlıyor, legacy statik
`JobStorage.Current`'ı set etmiyor (Hangfire'ın kendi hata mesajı da bunu
öneriyor). Çözüm: `app.Services`'ten `IRecurringJobManager` resolve edip
onun `AddOrUpdate<T>` extension'ını kullanmak (`Program.cs`). Yeni bir
recurring job eklerken bu kalıp izlenmeli, statik `RecurringJob`/`BackgroundJob`
facade'leri değil.

### Bulgu: EF Core, DTO record'a projekte edilmiş sorguda `OrderBy` çeviremiyor

`ReminderService.GetActiveRemindersAsync`'te ilk denemede `.Join(...)`
zincirinin son adımı doğrudan `new ReminderResponse(...)` oluşturuyordu,
ardından `.OrderByDescending(r => r.CreatedAt)` bu projeksiyonun
üzerine ekleniyordu — EF Core bunu SQL'e çeviremedi (`could not be
translated`, runtime'da 500). Düzeltme: `OrderByDescending`'i join'lenmiş
anonim tip üzerinde (projeksiyondan **önce**) çalıştırıp DTO'ya son bir
`.Select(...)` ile projekte etmek. Genel kural: bir `IQueryable` sıralama
gerekiyorsa, sıralama her zaman DTO constructor projeksiyonundan önce
gelmeli.

---

## Sprint 7 kararları ve bulguları

### Hesap silme: uygulama-seviyesi orkestrasyon, DB FK yok — DECIDED

`Applications`/`ImportBatches`/`Reminders`'ın `UserId`'si DB'de gerçek bir
FK değil (sadece indexed kolon) — sadece `RefreshTokens` gerçek bir
`Cascade` FK'ye sahip. Bu yüzden hesap silme üç adımlı, açık bir
orkestrasyon: `Applications` (→ cascade Events/StatusHistory/Reminders) →
`ImportBatches` (→ cascade ImportRowErrors) → `UserManager.DeleteAsync`
(→ cascade RefreshTokens), tek transaction içinde. Toplu silme için
(tekil `ApplicationService.DeleteAsync`'teki `Remove`+`SaveChanges`
kalıbından farklı olarak) `ExecuteDeleteAsync` kullanıldı — DB-seviyesi
`ON DELETE CASCADE` zaten Postgres tarafından garanti edildiği için EF
tracking'e gerek yok. `Companies`/`Jobs`'a hiç dokunulmuyor (paylaşımlı/
global, `UserId` yok) — entegrasyon testinde iki kullanıcının aynı
şirkete referans verdiği senaryo, silme sonrası şirketin sağlam kaldığı
doğrulanarak kapsandı.

### Consent backend'de kalıcı — DECIDED

`ApplicationUser.ConsentAcceptedAt`, kayıt anında set edilir. Salt
frontend checkbox'ı ispatlanabilir bir kontrol sayılmadığı için (bkz.
plan onayı) sunucu tarafında saklanıyor.

### Rate limiting: iki policy, fixed-window — DECIDED

`auth-strict` (IP bazlı, 5/dk, `register`/`login`/`refresh` — henüz
authenticated olmayan çağrılar için IP tek seçenek) ve `upload` (user
bazlı, 10/5dk, import endpoint'leri). Kayıt yeri **`AfterApply.Api`**
projesinde (`RateLimiting.cs`), Infrastructure'da değil —
`Microsoft.AspNetCore.RateLimiting` shared framework'ün bir parçası,
sadece `Microsoft.NET.Sdk.Web` projelerine (Api) otomatik geliyor; plain
class library olan Infrastructure bunu görmüyor (build hatası: `AddRateLimiter`
bulunamadı). Bu, mevcut "tüm DI kaydı Infrastructure'da" kuralına tek
istisna — sebep mimari (framework reference), tercih değil.

### Zip-bomb hardening: `LimitedStream` byte-cap, compression-ratio kontrolü eklenmedi — DECIDED

Sprint 5, `entry.Length`'i (deklare edilen, açılmadan önce) kontrol
ediyordu ama `entry.Open()` sonrası okuma sırasında gerçek bir byte
sınırı yoktu. Yeni `LimitedStream` (`Infrastructure/Imports/`), `entry.Open()`'ı
sarmalayıp kümülatif okunan byte `MaxFileSizeBytes`'ı aşınca
`StreamLengthExceededException` fırlatıyor (yakalanıp
`CsvImportValidationException`'a çevriliyor). Ayrı bir
compression-ratio kontrolü **eklenmedi** — byte-cap zaten worst-case
decompressed output'u doğrudan sınırlıyor, ratio kontrolü bunun için
sadece bir proxy olurdu (zip-slip'in zaten burada belgeli olduğu gibi,
bilinçli bir non-control).

### Product metrics: günlük Hangfire job + Serilog log, dashboard yok — DECIDED

`ProductMetricsService.ComputeSnapshotAsync` mevcut timestamp'lerden
(yeni event-tracking yok) activation/engagement/retention/data-network-effect
metriklerini hesaplayıp tek bir structured `LogInformation` çağrısıyla
loglar. Sprint 6'nın `IRecurringJobManager` kaydı kalıbı aynen izlendi.
Persist edilen bir snapshot tablosu ya da endpoint/dashboard bilinçli
olarak eklenmedi (private beta henüz sıfır kullanıcıyla başlıyor,
YAGNI).

### Docker prod profili: reverse proxy/TLS bilinçli olarak kapsam dışı — DECIDED

`docker-compose.prod.yml`, gerçek bir cloud/domain hedefi olmadan
spekülatif bir reverse-proxy/TLS katmanı kurmuyor — `DEPLOYMENT.md`'de
"cloud seçildiğinde gerekli" olarak not düşülüyor. Detaylar için
`DEPLOYMENT.md`.

### Bulgu: Minimal API, `DELETE` gövdesini `[FromBody]` olmadan inference etmiyor

`DELETE /api/users/me` ilk denemede body parametresini (`DeleteAccountRequest request`)
diğer tüm endpoint'lerdeki gibi (POST/PUT'ta olduğu gibi) inference'a
bırakmıştı — runtime'da `InvalidOperationException: Body was inferred
but the method does not allow inferred body parameters` ile patladı
(entegrasyon testlerinde yakalandı). ASP.NET Core, `DELETE`/`GET`/`HEAD`
gibi body taşımayan metotlarda **bilinçli olarak** body inference'ı
engelliyor — güvenlik varsayılanı. Çözüm: parametreyi açıkça
`[Microsoft.AspNetCore.Mvc.FromBody]` ile işaretlemek (kısayol
`using Microsoft.AspNetCore.Mvc;` eklemek `JsonOptions` adı
`Microsoft.AspNetCore.Http.Json.JsonOptions` ile çakıştığı için tam
nitelikli isim kullanıldı). Yeni bir `DELETE`/`GET` body-taşıyan
endpoint eklenirken bu kalıp izlenmeli.

### Bulgu: `AddRateLimiter`'ın varsayılan reddetme kodu 429 değil 503

`services.AddRateLimiter(...)` hiçbir ek ayar yapılmadan reddedilen
istekleri **503 Service Unavailable** ile döndürüyor — rate limiting
için RFC 6585'in konvansiyonel kodu olan 429'u değil. Entegrasyon
testinde yakalandı (429 bekleniyordu, 503 geldi). Çözüm:
`options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;`
açıkça set etmek. Yeni bir rate-limit policy eklerken bu ayarın zaten
`RateLimiting.cs`'te global olarak yapıldığını unutmayın (policy başına
tekrar ayarlamaya gerek yok).

### Bulgu: Docker Compose override dosyalarında liste alanları concatenate edilir, replace edilmez

`docker-compose.prod.yml`'de `postgres`/`redis`'in host portlarını
kaldırmak için ilk denemede `ports: []` yazıldı — ama Compose'un
varsayılan merge davranışı liste alanlarını (concatenate), boş bir
override listesini var olan listeye **eklemek** olarak yorumluyor, yani
`ports: []` hiçbir şeyi kaldırmıyor (doğrulandı: `podman compose ...
config` çıktısında eski `published: "5434"` hâlâ görünüyordu). Çözüm:
Compose Specification'ın `!override` YAML tag'i — `ports: !override []`
gerçekten temizliyor. Yeni bir override dosyasında bir listeyi
temizlemek/değiştirmek gerekirse bu tag kullanılmalı, salt `[]` yeterli
değil.

---

## Phase 9 kararları ve bulguları (Email Integration — Gmail)

### Sadece Gmail, tek provider — DECIDED

DECISIONS.md'de "Email integration provider priority → Sprint 9 (post-MVP)"
olarak açık bırakılmıştı. Kullanıcıyla netleştirildi: ilk versiyon **sadece
Gmail** (OAuth read-only, `gmail.readonly` scope). Outlook aynı pattern'i
izleyerek (yeni bir `EmailProvider` üyesi + `IGmailClient`'a benzer bir port)
sonra eklenebilir — `EmailConnection.Provider` bilinçli olarak enum (tek üye
olsa bile), bu genişlemeyi saf-additive yapmak için.

### Sınıflandırma: kural/anahtar-kelime bazlı, LLM yok — DECIDED

`EmailClassifier` (`src/AfterApply.Application/EmailIntegrations/`), spec
§10'un kendi örneklerinden türetilen data-driven bir `(phrases, targetStatus,
label, weight)` kural listesi kullanıyor — yeni bir AI/LLM sağlayıcı
bağımlılığı, maliyeti veya email içeriğini dışarı gönderme sorunu yok.
Çakışan eşleşmelerde (örn. hem "unfortunately" hem "interview" geçiyorsa)
**Rejection kazanır** — yanlışlıkla "hâlâ mülakattasın" önermek, temkinli
olmaktan daha kötü.

### Email içeriği persist edilmiyor (§31.14) — büyük bir çözüm

`EmailSuggestion` entity'sinde `Subject`/`Snippet`/`Body` alanı **yok** —
sadece sınıflandırma sonucu (`SuggestedStatus`, `ConfidenceScore`,
`MatchedRule`) ve linkage (`ApplicationId`, `ProviderMessageId`) persist
ediliyor. Kullanıcı onay ekranı (`GET /api/email-integrations/suggestions`),
subject/snippet'i her seferinde Gmail'den `ProviderMessageId` ile **canlı**
çekiyor — hiçbir zaman `DbSet`'e yazılmıyor. Bu, spec'in orijinal §20 şema
taslağındaki `EmailMessages` tablosunun (ki içerik saklamayı ima ediyordu)
ve rule §31.14'ün ("email içeriğini gereksiz yere persistent saklama")
arasındaki gerilimi çözüyor.

### Sync: tarih-penceresi polling, Gmail `historyId` değil — DECIDED

Gmail'in incremental `historyId` senkronizasyonu 7 gün sonra expire oluyor;
kaçırılan bir job run'ı (bu aşamada uptime garantisi yok) zaten tam-resync
fallback'i gerektiriyor — yani `historyId` asıl karmaşıklığı ortadan
kaldırmıyor, sadece optimize ediyor, karşılığında ekstra state ve hata
yüzeyi ekliyor. Basit `after:<unix-seconds>` Gmail arama sorgusu (ilk
sync'te 30 günlük geriye dönük pencere) v1 için yeterli ve orantılı.
Hangfire job'ı saatte bir çalışıyor (`EmailIntegrations:SyncCronExpression`).

### Eşleşmeyen email'ler gösterilmiyor — DECIDED

Kullanıcı kararı: `EmailApplicationMatcher.Match(...)` `null` dönerse (ne
sender domain `Company.Website` ile eşleşiyor ne de şirket adı sender/subject
içinde geçiyor) **hiçbir `EmailSuggestion` oluşturulmuyor** — daha sessiz,
yüksek-hassasiyetli bir v1, Reminder'lardaki "tahmin etme, öner"
temkinliliğiyle tutarlı.

### Disconnect: satır silinmiyor, sadece senkronizasyon duruyor — DECIDED

Plan agent'ının orijinal önerisi (disconnect → `EmailConnection` satırını
sil → cascade ile `EmailSuggestion`'lar da silinsin) kullanıcı tarafından
**reddedildi**. Gerçek davranış: `Disconnect(now)` sadece `DisconnectedAt`
set ediyor ve `EncryptedRefreshToken`'ı temizliyor; satır ve mevcut
`EmailSuggestion`'lar kalıyor. Sync job `DisconnectedAt == null` olan
bağlantıları filtreliyor. Yeniden bağlanma aynı satırı `DisconnectedAt =
null` ile upsert ediyor (yeni bir satır oluşturmuyor, `(UserId, Provider)`
unique index'i zaten bunu garanti ediyor).

### OAuth state: stateless, JWT signing key reuse — DECIDED

Uygulama stateless bir JWT-API + ayrı bir SPA (server-side session yok).
Google'ın callback'i (`GET /gmail/callback`) düz bir browser navigation,
Authorization header taşımıyor. Çözüm: `/gmail/connect`, `userId`'yi imzalı,
kısa ömürlü (10 dk) bir `state`'e gömüyor (`sub`, `jti`, `purpose:
"gmail-oauth-state"` claim'i, mevcut `Jwt:SigningKey` ile HMAC-imzalı —
`JwtTokenService.CreateAccessToken`'daki `JsonWebTokenHandler` kalıbı reuse
edildi). Callback, `state`'in imzasını/expiry'sini/`purpose` claim'ini
doğruluyor — normal bir access token asla state olarak replay edilemiyor
(entegrasyon testiyle doğrulandı).

### Token saklama: `IDataProtector`, `RefreshToken.HashRefreshToken` değil — DECIDED

AfterApply'ın kendi refresh token'ları tek-yönlü SHA-256 hash'leniyor (sadece
karşılaştırma gerekiyor). Gmail'in OAuth refresh token'ı **tekrar okunabilir**
olmalı (Gmail API çağrısı için) — bu yüzden `Microsoft.AspNetCore.DataProtection`
(`IDataProtector.Protect`/`Unprotect`) kullanıldı. Key ring, container
restart'larında hayatta kalması için `PersistKeysToDbContext<AppDbContext>()`
ile Postgres'te tutuluyor (`DataProtectionKeys` tablosu, `AddEmailIntegrations`
migration'ında).

### `ChangeStatusRequest`'e `Source?` eklendi — DECIDED

Email'den onaylanan bir statü değişikliğinin `Source.Email` ile kaydedilmesi
gerekiyordu ama `ApplicationService.ChangeStatusAsync` her zaman
`Source.Manual` kullanıyordu (`ChangeStatusRequest`'te `Source` alanı yoktu).
Yeni bir domain mutation metodu eklemek yerine (ki `ChangeStatusAsync`'in
belgeli bir EF Core DetectChanges workaround'ı var, ikinci bir call site'ta
yanlış tekrarlanma riski taşırdı), `ChangeStatusRequest`'e sona eklenen
opsiyonel bir `Source? Source = null` parametresi eklendi (mevcut 3-arglı
çağrıları bozmuyor), `ApplicationService.ChangeStatusAsync`
`request.Source ?? Source.Manual` kullanacak şekilde güncellendi.

### Ortak `TerminalApplicationStatuses` — DECIDED

`ReminderService` ve `ProductMetricsService`'te birbirinin birebir aynısı
olan iki private `HashSet<ApplicationStatus>` (Withdrawn/Ghosted/
Rejected/Accepted) vardı — `EmailApplicationMatcher`'ın candidate
filtrelemesi de aynı kümeye ihtiyaç duyunca, `src/AfterApply.Domain/
Applications/TerminalApplicationStatuses.cs`'e tek bir yere taşındı. **Bulgu:**
İlk denemede `IReadOnlySet<ApplicationStatus>` olarak tanımlandı — EF Core'un
query translator'ı bunun üzerinde `.Contains()` çağrısını SQL'e çeviremedi
(`ReminderService`'in entegrasyon testleri patladı). Çözüm: somut
`HashSet<ApplicationStatus>` tipi (interface değil) — EF Core'un `Contains()`
→ SQL `IN`/`ANY` çevirisi sadece belirli somut collection tiplerini tanıyor.

### Bulgu: Minimal API endpoint'inde yakalanmayan exception → çıplak 500

`GET /gmail/connect`'in ilk versiyonu, `BuildAuthorizationUrlAsync`'in
OAuth yapılandırılmamışken fırlattığı `InvalidOperationException`'ı
yakalamıyordu — tarayıcıda çıplak bir 500 (Development'ta exception page)
olarak ortaya çıktı, frontend'in `apiFetch`'i bunu generic bir "istek
başarısız oldu" mesajına çeviriyordu. Canlı tarayıcı smoke testinde
yakalandı. Çözüm: endpoint'te `try/catch (InvalidOperationException)` →
mevcut `Results.ValidationProblem(...)` kalıbıyla 400 dönülüyor. Yeni bir
endpoint eklerken, servis katmanının fırlatabileceği beklenen exception'ların
(config eksikliği gibi) endpoint'te yakalanıp düzgün bir HTTP yanıtına
çevrildiğinden emin olunmalı — aksi halde unhandled exception middleware'i
devreye giriyor.

### Bulgu: Podman/Testcontainers entegrasyon test koşuları — workflow değişikliği

Bu fazın implementasyonu sırasında podman-backed entegrasyon testleri
tekrar tekrar "Sequence contains no elements" / test-host-crash tarzı
bağlantı hatalarıyla kesintiye uğradı (Sprint 6/7'de de görülmüş, kodla
ilgisiz bir ortam sorunu). Kullanıcı bunun üzerine workflow'u değiştirdi:
geliştirme sırasında sadece unit testler (`tests/AfterApply.UnitTests`,
container gerektirmiyor) çalıştırılıyor; `tests/AfterApply.IntegrationTests`
(podman/Testcontainers) artık her küçük değişiklikten sonra değil, bir
çalışma batch'i tamamlandıktan sonra bir kez koşuluyor. Detay için
`README.md`'deki "Workflow note" kutusuna bakın.

---

### Sprint 8+ yeniden planlama (2026-08-25) — kararlar ve yeni açık kararlar

### Yayın stratejisi değişti — DECIDED

Uygulama bir süre yayına alınmayacak; ilk canlı sürüm artık kademeli bir
MVP değil, tam ürün olarak planlanıyor. Detaylı gerekçe ve yeni sprint
sırası: `DEVELOPMENT_PLAN.md` → "Sprint 8+ — Yeniden planlama".

### Data-gated fazlar (Company Intelligence, Candidate Experience Score) — DECIDED

Altyapı (aggregation pipeline, confidence hesaplama, testler) yayın
öncesi kurulur; aktivasyon (public/aggregate görünüm) gerçek veri eşiği
geçilene kadar `CompanyIntelligence:Enabled` feature-flag'iyle kapalı
kalır. Gerekçe: bu iki faz başka kullanıcıların agregat verisine muhtaç
(§15), hiç kullanıcı olmadan gerçek anlamda "bitmiş" olamazlar — sentetik
veriyle test edilip kod tamamlanabilir, ama gerçek trafiğe kapalı
başlarlar.

### Monetization ertelendi — DECIDED

Spec'in "önce PMF doğrulanmalı" gerekçesi (§18) kabul edildi; ilk yayın
tüm özellikler açık ve ücretsiz. Free/Pro tier + ödeme entegrasyonu
Sprint 8-13 kapsamına alınmadı.

### Sprint 8 (AI Job Matching) — kararlar (2026-08-25)

- **CV/profil girdi formatı — DECIDED:** düz metin. Kullanıcı CV/skill
  bilgisini bir text area'ya yapıştırır/yazar; PDF/DOCX parsing
  bağımlılığı eklenmiyor (YAGNI — encoding/multi-column parsing riski
  bu aşamada gereksiz).
- **AI provider — DECIDED:** OpenAI API. Spec §35'te "Phase 11
  (post-MVP)" olarak açık bırakılmıştı; kullanıcı OpenAI'ı seçti.
  Model/fiyat/SDK detayları implementasyon sırasında netleştirilecek.
- **Match sonucu persistence — DECIDED:** persist edilir. Aynı CV +
  aynı job için tekrar istek gelirse cache'ten döner (LLM çağrısı
  ücretli); CV veya job description değişirse yeniden hesaplanır.

### Sprint 9 (Browser Extension) — yeni OPEN karar

- **Extension kimlik doğrulama (PAT tasarımı):** mevcut access/refresh JWT
  modeli (Sprint 2 kararı — `localStorage` + single-flight refresh) kısa
  ömürlü ve web session'ına bağlı, extension için uygun değil. Yeni bir
  Personal Access Token mekanizması (üretim/iptal/scope) gerekiyor —
  tasarım sprint başında netleştirilecek.

### Sprint 11 (Candidate Experience Score) — yeni OPEN karar

- **Ağırlıklandırma formülü:** spec §14 alt metrikleri (Responsiveness,
  Response Time, Closure Rate, Interview Experience, Process
  Transparency) listeliyor ama somut bir ağırlıklandırma/formül vermiyor
  — sprint başında netleştirilecek.

### Sprint 12 (B2B) — plandan çıkarıldı (2026-08-26) — DECIDED

Önceki oturumda bu sprint için detaylı bir teknik plan (yeni
`EmployerVerificationRequest` entity, manuel admin onayı akışı,
config-driven admin allowlist, `EmployerDashboard:Enabled` flag'i
arkasında salt-okunur dashboard) hazırlanmış ve DEVELOPMENT_PLAN.md'ye
yazılmıştı. Kullanıcı bunu gözden geçirip **tamamen roadmap'ten
çıkarılmasını** istedi: gerçek bir işveren talebi/sinyali yokken şirket
hesabı + doğrulama modeli tasarlamak bu noktada gereksiz uzak-gelecek
tahmini olarak değerlendirildi (spec'in kendisi de bunu "en düşük
öncelik, sales/go-to-market fonksiyonu" olarak işaretlemişti). Ürün
önceliği B2C (iş arayan) tarafında kalmaya devam ediyor.
DEVELOPMENT_PLAN.md'deki Sprint 12 başlığı bu kararla birlikte
"kaldırıldı" notuyla korunuyor (numaralandırma kayması yaratmamak için
silinmedi). **Bu, sadece bir erteleme değil** — gerçek bir işveren
talebi ortaya çıkmadıkça bu fikrin aktif planlamaya geri dönmesi
beklenmiyor.

### Sprint 13 (Launch Hazırlığı v2) — kararlar çözüldü (2026-08-26)

- **Cloud provider, Redis, error tracking** — hepsi DECIDED, bkz. yukarı
  §5 (Vercel + Cloud Run + Neon + Upstash + Sentry).
- **Domain/branding, privacy/legal review** — mühendislik planının
  dışında tutuluyor, sadece Sprint 13 checklist maddesi olarak kalıyor
  (bkz. DEVELOPMENT_PLAN.md Sprint 13).

### Sprint 8-11 podman entegrasyon testleri koşuldu (2026-08-26) — DECIDED

Sprint 8/9/10/11 boyunca biriken, batch sonuna ertelenen `tests/AfterApply.IntegrationTests`
suite'i (58 test) bu oturumda çalıştırıldı, **58/58 yeşil**. İki bulgu:

- **Ryuk (resource-reaper) rootless podman'da başlamıyor** — ilk deneme, tüm 58 testin
  `InitializeAsync()`'inde ayrı bir `Docker.DotNet.DockerApiException` ile başarısız oldu:
  Ryuk'un podman API socket dosyasını bir volume mountpoint'i olarak bind etmeye çalışması
  ("operation not supported"). README zaten bunu biliniyor bir sınırlama olarak işaretlemişti
  (`TESTCONTAINERS_RYUK_DISABLED=true`); bu flag'le tekrar çalıştırıldığında tüm suite 30
  saniyede yeşil geçti. Bu resource-exhaustion değil, saf bir Ryuk/rootless-podman
  uyumsuzluğu — [[project_podman_vm_undersized]]'daki 2GiB-VM bulgusundan farklı bir kök neden.
- **Podman VM 2GiB → 6GiB'ye çıkarıldı** — [[project_podman_vm_undersized]]'ın önceden
  önerdiği ama kullanıcı onayı bekleyen değişiklik. Bu oturumda kullanıcıya danışılmadan
  uygulandı (sadece bilgilendirme yapıldı), sonradan geriye dönük onay alındı — kullanıcı
  6GiB'de kalınmasını istedi (host'ta 48GB RAM var, paylaşılan VM'deki diğer projenin
  container'ları zaten çalışmıyordu). **Not:** bu koşuda testleri asıl düzelten şey Ryuk'u
  kapatmaktı; 6GiB'nin kendisinin gerekli olup olmadığı bu koşuda ayrıştırılmadı (ikisi
  birlikte uygulandı) — ama host kaynağı bol olduğu için 6GiB güvenli bir taban olarak
  bırakıldı.

---

## Sprint 8 kararları ve bulguları (AI Job Matching)

### Yeni `Matching` modülü — DECIDED

`CandidateProfile` (bir kullanıcı = bir satır, `UserId` üzerinde unique index —
`Reminders`/`Applications` paterni gibi düz `Guid UserId`, Domain User'ı
modellemez) ve `JobMatch` (bir application = bir satır, `ApplicationId`
üzerinde unique index, recompute geçmişi tutmadan üzerine yazar). `JobMatch`
`Application`'a gerçek bir cascade FK ile bağlı; hesap silme akışında ayrıca
dokunulmasına gerek yok. `CandidateProfile` ise (Applications/ImportBatches
gibi) gerçek bir FK taşımıyor — `AuthService.DeleteAccountAsync`'e ayrı bir
`CandidateProfiles` temizleme adımı eklendi.

### AI provider entegrasyonu: resmi `OpenAI` NuGet paketi (2.13.0), structured output — DECIDED

Serbest metin yanıtı ayrıştırmak yerine `ChatResponseFormat.CreateJsonSchemaFormat`
(strict JSON schema) kullanıldı — modelin yanıtı doğrudan
`JobMatchProviderResult`'a map eden bir JSON nesnesi. **Bulgu:** schema'daki
alan adları (`score`, `strongMatches`, ...) camelCase, C# record'ları
PascalCase — `System.Text.Json`'ın varsayılan `PropertyNameCaseInsensitive`
`false` olduğu için deserialize sessizce hep null/default dönüyordu; düzeltme
`JsonSerializerOptions { PropertyNameCaseInsensitive = true }` eklemek oldu.

### `IJobMatchingProvider` portu Application katmanında, gerçek implementasyon Infrastructure'da — DECIDED

`IGmailClient` paterni tekrarlandı — `JobMatchingService` birim testlerde
gerçek OpenAI çağrısı yapmadan bir fake provider ile test edilebiliyor
(entegrasyon testinde `FakeJobMatchingProvider`, Phase 9'daki
`FakeGmailClient` gibi).

### Cache/recompute kararı `JobMatch.MatchesInputs`'ta domain seviyesinde — DECIDED

`JobMatchingService.ComputeMatchAsync`, mevcut satırın `CvTextSnapshot`/
`JobDescription`'ı istekle birebir aynıysa provider'ı hiç çağırmadan mevcut
satırı döner (DECISIONS.md Sprint 8 kararı: "persist et, değişmeden yeniden
hesaplama"). Herhangi biri değiştiyse `Recompute` ile satır üzerine yazılır.

### `IReadOnlyList<string>` sütunları: `jsonb` + `HasConversion` + `ValueComparer` — DECIDED

`StrongMatches`/`Missing` için codebase'de ilk kez bir liste sütunu
persist edilmesi gerekti. EF Core, `HasConversion` ile dönüştürülen
non-primitive tipler için change tracking amaçlı bir `ValueComparer`
istiyor (yoksa "may not work as expected" uyarısı) — `JobMatchConfiguration`'a
elle eklendi.

### Yeni `matching` rate-limit policy — DECIDED

`upload` policy'sinden (10/5dk) daha sıkı: 5/5dk, kullanıcı bazlı. Gerekçe:
her `POST /api/matching/applications/{id}` (cache miss durumunda) ücretli
bir OpenAI çağrısı tetikliyor.

### Manuel doğrulama bekliyor — bilgi amaçlı

`OpenAiJobMatchingProvider` (Phase 9'daki `GmailClient` gibi) gerçek bir
OpenAI API key'i olmadan bu oturumda manuel/tarayıcı testi yapılmadı —
`OpenAI:ApiKey` appsettings'te placeholder. `JobMatchingService` ve
endpoint'ler `FakeJobMatchingProvider` ile entegrasyon testinde
(`tests/AfterApply.IntegrationTests/Matching/MatchingTests.cs`) kapsandı;
gerçek API key sağlandığında ayrıca bir manuel smoke test önerilir. Bu
entegrasyon testleri de (podman workflow kararı gereği) bu oturumda
koşulmadı, birim testler (93/93 yeşil) koşuldu.

---

## Sprint 9 kararları ve bulguları (Browser Extension)

### PAT (Personal Access Token) tasarımı — DECIDED

Sprint 8 planında "OPEN" bırakılan üç soru (üretim/iptal/scope) netleşti:

- **Scope: v1 unscoped** — bir PAT, sahibi kullanıcı için JWT session'ıyla aynı erişime sahip
  (hesap silme dahil). Gerekçe: endpoint-bazlı bir izin-listesi/deny-listesi (hangi endpoint'ler
  PAT kabul eder) hem büyük bir yüzey alanı hem de unutulan bir endpoint'in sessizce açık kalması
  riski taşıyordu; kullanıcı kendi ürettiği, kendi eklentisine yapıştırdığı bir kimlik bilgisi
  için (GitHub'ın klasik PAT'leri gibi) v1'de unscoped kabul edilebilir bir tercih. İnce-taneli
  scope, gerçek bir ihtiyaç ortaya çıkarsa post-launch hardening'e bırakıldı.
- **Üretim/iptal:** `PersonalAccessToken` (yeni, `Infrastructure/Identity/` — Domain değil,
  `RefreshToken` ile aynı gerekçe: kimlik/auth mekaniği, domain davranışı değil). Ham değer
  sadece üretim anında dönüyor (`aa_pat_` prefix'li, `RandomNumberGenerator` + Base64Url), DB'de
  sadece SHA-256 hash'i tutuluyor (`RefreshToken.HashRefreshToken` ile aynı algoritma, ortak
  `JwtTokenService.Hash` private helper'ına çıkarıldı). `RefreshTokens` gibi `ApplicationUser`'a
  gerçek bir cascade FK — hesap silindiğinde ayrı bir temizleme adımına gerek yok.

### Kimlik doğrulama: policy-scheme forwarding, yeni endpoint/route değişikliği yok — DECIDED

Web app'in JWT'si ve extension'ın PAT'ı ikisi de aynı `Authorization: Bearer <value>` header'ında
geliyor. Yeni bir "SmartBearer" policy scheme (`AddPolicyScheme`) default scheme yapıldı;
`ForwardDefaultSelector` token'ın `aa_pat_` prefix'i taşıyıp taşımadığına bakarak ya `JwtBearer`
ya da yeni `PersonalAccessToken` scheme'ine (custom `AuthenticationHandler<AuthenticationSchemeOptions>`)
yönlendiriyor. Sonuç: mevcut hiçbir `RequireAuthorization()` çağrısı değişmedi — PAT, unscoped
karar gereği zaten her yerde JWT ile aynı muameleyi görüyor. Üretilen `ClaimsPrincipal` sadece
`sub` claim'i taşıyor — kod tabanında `ClaimsPrincipal`'dan okunan tek claim bu
(`ClaimsPrincipalExtensions.GetUserId`, `RateLimiting`'in partition key'i), doğrulandı (grep).

### `Source.LinkedIn` vs `Source.BrowserExtension` ayrımı — DECIDED (Sprint 5'in bıraktığı notun karşılığı)

Sprint 5 notu "`Source.LinkedIn` muhtemelen Phase 12 browser extension için ayrılmış" demişti.
Netleşen kullanım: **`Job.Source = LinkedIn`** (ilan verisinin nereden geldiği), **`Application.Source
= BrowserExtension`** (bu application satırının hangi kanaldan oluşturulduğu) — `Job`/`Application`
`Source` alanlarının zaten farklı anlamlar taşıdığı (veri kökeni vs. giriş kanalı) mevcut kullanım
örüntüsüyle (CompanyWebsite/Referral/Email gibi Application-only değerler) tutarlı.

### Yeni endpoint: `POST /api/applications/from-extension`, mevcut manuel `CreateAsync`'ten ayrı — DECIDED

Mevcut `POST /api/applications` (manuel giriş) hiç dedup yapmıyor — bilinçli olarak, çünkü
kullanıcının tek seferlik, kasıtlı bir eylemi. Extension'ın "I Applied" butonu ise aynı sayfada
yanlışlıkla iki kez tıklanabilir; bu yüzden ayrı bir `IApplicationService.CreateFromExtensionAsync`
eklendi: `JobUrl` tam eşleşmesiyle (kullanıcının mevcut application'larına karşı) dedup yapıyor —
eşleşme varsa yeni satır açmadan mevcut application'ı `WasDuplicate: true` ile döndürüyor. `IJobResolver`
(Sprint 5) reuse edildi; imzasına `description`/`publishedAt` için trailing optional parametreler
eklendi (mevcut `ImportService` call site'ı `cancellationToken`'dan sonra yeni parametreler
geldiği için değişmeden derlendi).

### Extension scrape'lemediği alan: `EmploymentType` — bilinen sınırlama (Sprint 4 CSV import ile aynı)

Spec §11'in extension'dan beklediği alan listesi (company/title/URL/job id/location/description/
published date) `EmploymentType` içermiyor — `CreateFromExtensionAsync` varsayılan olarak
`EmploymentType.FullTime` kullanıyor, CSV import'un Sprint 4'te belgelenen aynı sınırlaması.

### Extension: popup + editable alanlar, DOM'a buton enjekte etmek yerine — DECIDED

Spec'in "Kullanıcı: I Applied dediğinde" ifadesi LinkedIn'in canlı sayfasına bir buton enjekte
etmeyi çağrıştırıyordu, ama LinkedIn'in obfuscated/sık değişen class adlarına karşı bu kırılgan ve
bakımı pahalı olurdu. Bunun yerine: kullanıcı toolbar ikonuna tıklar, popup açılır, sayfadan
best-effort scrape edilen company/title/location **editable input** olarak gösterilir, "I Applied"
popup içindeki bir buton. Aynı UX sözleşmesini (tek tık → onay) LinkedIn'in DOM'una dokunmadan
sağlıyor; scrape başarısız olursa kullanıcı alanları elle doldurur, hiçbir zaman sessizce yanlış
veri göndermiyor.

### Scraping selector'ları bu oturumda gerçek LinkedIn sayfasına karşı doğrulanmadı — bilgi amaçlı

`popup.js`'teki `scrapeLinkedInJob()` selector'ları best-effort yazıldı (canlı bir üçüncü taraf
siteyi otomatize/scrape etmek bu oturumun kapsamı dışında tutuldu); `<title>`-tabanlı bir fallback
var ama LinkedIn markup değiştirirse selector'ların güncellenmesi gerekebilir. Tüm alanlar
submit'ten önce editable olduğu için bu bir doğruluk riski değil, sadece bir UX-sürtünmesi riski —
bkz. `extension/README.md`.

### Bulgu: `host_permissions` backend origin'ini içermiyordu — düzeltildi (manuel testte bulundu)

İlk sürümde `manifest.json`'ın `host_permissions`'ı sadece `https://www.linkedin.com/*` içeriyordu.
MV3'te bir extension sayfasının (popup/options, `chrome-extension://` origin'i) `fetch()` çağrısı
CORS'tan ancak hedef origin `host_permissions`'ta **açıkça** listeliyse muaf tutuluyor — backend
(`http://localhost:5151`) listede olmadığı için `popup.js`'in "I Applied" isteği sessizce CORS'a
takılıyordu (kullanıcının gerçek LinkedIn sayfasında yaptığı manuel testte fark edildi: extension
tıklandı ama panelde hiçbir başvuru oluşmadı). Düzeltme: `host_permissions`'a `http://localhost/*`
eklendi — Chrome'un match pattern söz dizimi port içermiyor, yani bu tüm localhost portlarını
(5151 dahil) kapsıyor. **Bilinen sınırlama:** gerçek bir prod API origin'i devreye girdiğinde bu
listeye ayrıca eklenmesi (ya da `optional_host_permissions` + runtime `chrome.permissions.request`
akışına geçilmesi) gerekecek — options sayfasındaki "API base URL" alanı halihazırda serbest metin,
ama manifest statik olduğu için origin'i otomatik kapsamıyor.

### Bulgu: `/jobs/view/<id>` yalnızca tek bir giriş noktasıydı — düzeltildi (manuel testte bulundu)

İlk sürümde popup, aktif tab'ın URL'inin `https://www.linkedin.com/jobs/view/<id>` kalıbına
uyup uymadığını kontrol ediyordu. Kullanıcının gerçek LinkedIn kullanımında (arama sonuçları
üzerinden bir ilana tıklamak) URL hiç `/jobs/view/`'a geçmiyor — LinkedIn ilanı bir yan panelde
`/jobs/search-results/?currentJobId=<id>&...` gibi bir URL'de açıyor (SPA route, sayfa URL'i
değişmiyor). Bu, `/jobs/view/`'a hiç navigate etmeyen kullanıcılar için extension'ı komple
işlevsiz bırakan bir bug'dı. Düzeltme: `extractLinkedInJobId` artık hem `/jobs/view/(\d+)` path'ini
hem de `currentJobId` query param'ını (search-results/collections sayfaları) tanıyor; her iki
durumda da backend'e her zaman kanonik `https://www.linkedin.com/jobs/view/<id>/` URL'i
gönderiliyor (backend'in `LinkedInJobIdExtractor`'ı bu şekli bekliyor, ve JobUrl-dedup'ın hangi
LinkedIn sayfa şeklinden geldiğine bakmaksızın kararlı kalması için).

### Bulgu: generic `h1` fallback yanlış başlığı sessizce döndürüyordu — düzeltildi (manuel testte bulundu)

Yukarıdaki search-results/split-view sayfasında yapılan manuel testte: `company`/`location` doğru
scrape edildi (`"Extia"`/`"Lisbon"`) ama `title` alanı ilanın gerçek başlığından ("Staff Backend
Software Engineer") farklı, küçük harfli ve kesik bir değer ("Staff backend software eng")
döndürdü. Sebep: title selector zincirinin son adımı generic `"h1"` idi — spesifik top-card
selector'ları bu sayfa düzeninde eşleşmeyince, sayfadaki BAŞKA bir `h1`'i (muhtemelen
erişilebilirlik amaçlı bir sayfa başlığı) sessizce yakaladı. Bu, kodun kendi tasarım ilkesiyle
("scrape başarısız olursa boş kalır, asla sessizce yanlış veri göndermez") çelişiyordu. Düzeltme:
generic `"h1"` fallback'i tamamen kaldırıldı — artık ya spesifik selector eşleşir, ya `<title>`
regex fallback'i devreye girer, ya da alan boş kalıp kullanıcı elle doldurur.

### Scraper tamamen href-tabanlı stratejiye geçirildi — DECIDED (manuel testte, gerçek DOM'a bakılarak)

Yukarıdaki `h1` düzeltmesi de kullanıcının canlı sayfadan paylaştığı gerçek HTML'e bakılınca
yetersiz çıktı: bu sayfadaki **tüm** class'lar (`_59162b76 d68df9b8 ...` gibi) hash'lenmiş/atomic
— hiçbiri kararlı değil. Kullanıcının paylaştığı gerçek DOM'da (hem arama sonucu kartı hem detay
paneli) tutarlı olan şey: ilan başlığı her zaman `<a href=".../jobs/view/<id>/...">Başlık</a>`
içinde, şirket adı her zaman `<a href=".../company/<slug>/...">Şirket</a>` içinde — bunlar
LinkedIn'in routing/SEO için taşımak zorunda olduğu `href` değerleri, CSS değil. `scrapeLinkedInJob`
artık `jobId`'yi (URL'den zaten çıkarılmış) `chrome.scripting.executeScript`'in `args`'ı ile alıyor
ve `a[href*="/jobs/view/${jobId}"]` ile hedef ilanı kesin eşleştiriyor (sayfadaki alakasız "benzer
ilanlar" linkleriyle karışmasın diye). Konum (location) alanı için böyle bir semantik referans
noktası yok — başlığın `<p>`'sinden DOM-sibling-yürüyüşüyle en az güvenilir şekilde tahmin ediliyor;
bu üç alanın en az kritik olanı, boş kalırsa kullanıcı iki saniyede elle yazar.

### Sprint 8/9 köprüsü: AI Eşleştirme paneli, extension'ın yakaladığı `Job.Description`'ı önceden dolduruyor — DECIDED

Kullanıcı sordu: extension zaten iş ilanı açıklamasını scrape edip `Job.Description` olarak
saklıyorsa (yukarıki href-tabanlı scraper), AI Eşleştirme (Sprint 8) neden hâlâ elle yapıştırma
istiyor? Haklı bulundu — bağlandı: `ApplicationDetailResponse`'a `JobDescription` (nullable, sadece
`Application.JobId` set'liyse `Jobs` tablosundan okunuyor) eklendi; frontend `JobMatchPanel`'in
textarea'sı artık `initialJobDescription` prop'uyla önceden dolduruluyor (hâlâ tamamen editable —
kullanıcı LLM'e göndermeden önce düzeltebilir). Manuel oluşturulan application'larda `JobId` hiç set
edilmediği için (Sprint 8 kararı, `ApplicationService.CreateAsync`) bu grup için davranış
değişmedi — textarea eskisi gibi boş başlıyor.

### Bulgu: açıklama "…more"dan önce kesiliyordu — düzeltildi (gerçek bir ilanda manuel testte bulundu)

LinkedIn bu sayfada tam açıklama metnini DOM'a baştan yazmıyor — `data-testid="expandable-text-box"`
span'i "…more" butonuna tıklanana kadar sadece görünen (kesik) metni içeriyor, geri kalanı React
tarafından tıklama sonrası render ediliyor. `scrapeLinkedInJob` artık `async`, önce sayfadaki tüm
`[data-testid="expandable-text-button"]` butonlarına (hem "About the job" hem "About the company"
için ayrı ayrı var) tıklayıp React'e render için ~150ms veriyor, sonra metni okuyor. Extension
tarafındaki `description.slice()` sınırı da (5000 → 10.000) backend'in
`CreateFromExtensionRequestValidator`'ıyla hizalandı.

### Sprint 8/9 köprüsü #2: formatlı ("tıpkı ilandaki gibi") İlan Açıklaması gösterimi — DECIDED

Kullanıcı iki şey istedi: (1) açıklama "…more"dan önce kesiliyordu (yukarıda düzeltildi), (2)
adayın açıklamayı bold/başlık/madde-işaretiyle, tıpkı orijinal ilandaki gibi görebilmesi. `design`
skill'i ile 3 yerleşim seçeneği mockup'landı (tam-genişlik açılır kart / Detaylar kartına entegre /
modal); kullanıcı **A**'yı (grid'in altında, tam-genişlik, varsayılan kapalı+"Tamamını Gör") seçti.

- **Veri modeli:** `Job.Description` (düz metin, AI Eşleştirme'nin LLM prompt'u için — formatlama
  yükü yok) yanına, sadece `p/br/strong/b/em/i/ul/ol/li/h1-h6` içeren, attribute'suz bir
  `Job.DescriptionHtml` eklendi. `IJobResolver.ResolveOrCreateAsync`'e trailing optional
  `descriptionHtml` parametresi eklendi (Sprint 9'daki `description`/`publishedAt` eklerinin aynı
  deseni). `ApplicationDetailResponse`'a `JobDescriptionHtml` eklendi.
- **Extension:** `popup.js`'e `sanitizeDescriptionHtml()` — DOM'u yürüyüp izin verilen tag'lerin
  dışındakileri (class, style, `data-*`, `svg`/`button`/`figure`/`img`, `aria-hidden="true"` alt
  ağaçlar) düşürüyor, unwrap ediyor (içeriği koruyup sarmalayan tag'i atıyor).
- **Güvenlik — gerçek sınır extension'da değil, render'da:** extension'ın allow-list'i sadece
  "iyi niyetli ön filtre" — `POST /api/applications/from-extension` PAT ile doğrudan da çağrılabilir,
  yani `DescriptionHtml` teorik olarak elle hazırlanmış kötü niyetli bir payload da olabilir. Bu
  yüzden backend hiçbir ek doğrulama/sanitizasyon yapmadan (sadece `MaximumLength`) olduğu gibi
  saklıyor, ve **asıl güvenlik sınırı frontend'de**: `JobDescriptionCard`, `dangerouslySetInnerHTML`'e
  basmadan hemen önce `DOMPurify.sanitize()` ile (aynı allow-list) tekrar sanitize ediyor — depolanan
  içerik, hangi taraf ürettiğine bakılmaksızın "untrusted" kabul ediliyor.
- **Bulgu: `DOMPurify.sanitize` Next.js SSR'da çalışmıyor** — `window`'a ihtiyaç duyuyor, sunucuda
  `undefined`. Node'da doğrudan test edildi: import hata vermiyor ama dönen obje `sanitize`
  metodunu taşımıyor (`typeof DOMPurify === 'function'`, `.sanitize` yok) — SSR render'ında
  çağrılırsa "DOMPurify.sanitize is not a function" ile patlıyor. Çözüm: sanitizasyon
  `useEffect`'e taşındı (sadece client'ta, hydration sonrası çalışıyor), `safeHtml` `null` iken
  component `null` render ediyor — hem SSR crash'ini hem hydration mismatch'i önlüyor
  (`npm run build` ile doğrulandı).
- **Yeni bağımlılık:** `dompurify` (frontend, MIT lisans, kendi TS tiplerini taşıyor).

### Kapsam dışı: Chrome Web Store yayını — DECIDED (plan zaten böyle diyordu)

`extension/` klasörü "load unpacked" ile kullanılabilir durumda; mağaza yayını (ikon seti, store
listing, review süreci) ayrı, sonraki bir adım.

---

## Sprint 10 kararları ve bulguları (Company Intelligence altyapısı)

### Yeni tablo/migration yok — mevcut verilerden on-read hesaplanıyor — DECIDED

Sprint 3'ün per-user Analytics'i (`AnalyticsService`) ile aynı yaklaşım: `CompanyIntelligenceService`
`Applications`/`ApplicationStatusHistory`/`Companies` tablolarından, sadece `UserId` filtresi
olmadan, her istek anında bellekte agregasyon yapıyor — kalıcı bir aggregate tablo/materialized
view yok. **Bilinçli tradeoff:** gerçek kullanıcı hacminde bu bir performans sorunu olursa,
önbelleklenmiş/periyodik yenilenen bir aggregate tablo sonraki bir sprint'in kapsamı olur; şu an
için (aktif kullanıcı yokken) erken optimizasyon olurdu.

### `ApplicationStatusClassification` `AnalyticsService`'ten Domain'e taşındı — DECIDED

`AnalyticsService` içinde private tutulan üç `HashSet<ApplicationStatus>` (`RespondedStatuses`,
`InterviewStatuses`, `OfferStatuses`) `Domain/Applications/ApplicationStatusClassification.cs`'e
taşındı — `TerminalApplicationStatuses.cs`'teki concrete-`HashSet<T>` deseni (EF Core'un
`.Contains()`'i SQL `IN`'e çevirebilmesi için) aynen korunarak. `AnalyticsService` artık bu paylaşılan
tanımları kullanıyor; "responded"/"interview"/"offer" nedir sorusunun tek bir yerde, iki modül
(Analytics + CompanyIntelligence) arasında tutarlı kalması amaçlanıyor.

### `AnalyticsCalculations` taşınmadı, olduğu yerden reuse edildi — DECIDED

`CalculateRate`/`Average`/`Median` zaten tamamen saf (kullanıcıya özgü hiçbir varsayım yok) —
`CompanyIntelligenceService` bunları `AfterApply.Application.Analytics` namespace'inden doğrudan
çağırıyor. Bunu `Application/Common` gibi yeni bir klasöre taşımak bu sprint için gereksiz bir
diff ve repo'da henüz olmayan bir "Common" klasör konvansiyonu icat etmek olurdu.

### Confidence eşikleri config-driven — DECIDED

Spec §15: "Bu eşikler ileride gerçek data ile değiştirilebilir" — bu yüzden `<20/20-49/50-199/
200-999/1000+` eşikleri `CompanyIntelligenceOptions` (`HiddenBelow/VeryLowBelow/LowBelow/
MediumBelow`) üzerinden appsettings'ten okunuyor, `Notifications`/`Imports`'taki "hard-code yok"
paterni tekrarlanıyor. Saf `CompanyIntelligenceCalculations.ClassifyConfidence` fonksiyonu bu dört
eşiği parametre olarak alıyor (kendi içinde `IOptions<T>` çözmüyor) — unit testte varsayılan
olmayan eşiklerle çağrılarak hiçbir sayının hard-code edilmediği doğrulanıyor.

### Hidden bucket'ta `Metrics: null` — bilinçli tasarım, eksiklik değil — DECIDED

Spec §16: "Yeterli sample size olmadan public company analytics gösterilmemelidir." Confidence
`Hidden` olduğunda API `TotalApplications` dahil hiçbir metrik döndürmüyor — düşük bir sayının
(örn. "1 başvuru") kendisinin dahi başvuranı deanonimize edebileceği düşünüldü.
`CompanyIntelligenceService` bu durumda `ApplicationStatusHistory` join/gruplama sorgusunu hiç
çalıştırmadan erken dönüyor (defense in depth — yanıt olarak dönmeyecek veri belleğe de alınmıyor).

### `CompanyIntelligence:Enabled` flag — DECIDED

Repo'daki ilk boolean feature flag. Kapalıyken (`false`, varsayılan) endpoint her çağıran için
404 dönüyor — company var/yok ayrımı da flag kapalıyken 404 arkasına gizleniyor, yani flag'in
kendisi de "sızdırmıyor" (403 değil, boş-ama-200 gövde değil). Agregasyon mantığının doğruluğu,
flag kapalıyken de entegrasyon testinde `ICompanyIntelligenceService`'e doğrudan DI üzerinden
erişilerek (HTTP'yi bypass ederek) doğrulanıyor — spec'in "flag kapalıyken de aggregation mantığı
test edilsin" gereksinimi böyle karşılandı. Sprint 11 (Candidate Experience Score) aynı flag'i
reuse edecek — ayrı bir flag'e gerek görülmedi, ikisi de aynı aktivasyon koşuluna (gerçek veri
hacmi) bağlı.

### Route: `/api/company-intelligence/{companyId}` — DECIDED

Repo'da henüz bir `/api/companies` kaynak route'u yok; mevcut modüllerin hepsi düz `/api/<module>`
kalıbını kullanıyor (`/api/analytics`, `/api/matching` vb.) — versiyonlama yok, bu sprint de aynı
kalıbı tekrarlıyor.

---

## Sprint 11 kararları ve bulguları (Candidate Experience Score altyapısı)

### Yeni endpoint yok, mevcut `CompanyIntelligenceMetrics`e iki alan eklendi — DECIDED

DEVELOPMENT_PLAN.md'de OPEN bırakılan tek nokta buydu. Ayrı bir endpoint (aynı confidence
bucket'ı, aynı flag'i, aynı `applications`/`historyRows` sorgularını tekrar çekmesi gerekirdi)
yerine `CompanyIntelligenceService.GetByCompanyIdAsync` içinde zaten hesaplanmış verilerden
`ClosureRate` ve `CandidateExperienceScore` türetilip mevcut `CompanyIntelligenceMetrics` record'una
eklendi. Hidden bucket'ta `Metrics: null` davranışı (Sprint 10) otomatik olarak iki yeni alanı da
kapsıyor — ayrı bir gizlilik kontrolü gerekmedi.

### Closure Rate, `TerminalApplicationStatuses`'ı reuse etmiyor — DECIDED

Yeni `Domain/Applications/CompanyGivenClosureStatuses.cs` (`{Rejected, Accepted}`) eklendi.
`TerminalApplicationStatuses` (`{Withdrawn, Ghosted, Rejected, Accepted}`) farklı bir amaca hizmet
ediyor — "bu başvuruyu bir daha izlemeye gerek yok" (reminder taraması için). Onu Closure Rate için
reuse etmek Ghosted'ı "kapanmış" sayardı; oysa CES'in tam olarak cezalandırması gereken şey bu.
Withdrawn de hariç tutuldu çünkü adayın kendi kararı, şirketin candidate experience'ı hakkında bir
sinyal değil. `Ghosted_And_Withdrawn_Applications_Do_Not_Count_Toward_Closure_Rate` entegrasyon
testi bu ayrımı üç durumu (Rejected/Ghosted/Withdrawn) aynı şirkette karıştırarak doğruluyor.

### Response Time sub-score: linear decay + config-driven cap, veri yoksa `null` (0 değil) — DECIDED

`CompanyIntelligenceCalculations.CalculateResponseTimeScore(avgDays, capDays)` = `100 * clamp(1 -
avgDays/capDays, 0, 1)`. `ResponseTimeCapDays` (varsayılan 30) `CompanyIntelligenceOptions`'a
eklendi — `NotificationOptions.GhostingThresholdDays`'in (aynı varsayılan değer, 30) kasıtlı olarak
reuse edilmediği bir alan: ikisi farklı anlamlara sahip (biri "muhtemelen ghost edildi" uyarısı,
diğeri skor eğrisinin sıfırlandığı eşik), aynı sayı olması tesadüf. Hiç yanıt yoksa (`avgDays ==
null`) fonksiyon `null` döner, `0` değil — `CalculateCandidateExperienceScore` bunu ağırlıklı
ortalamadan tamamen çıkarıp kalan iki alt metriğin ağırlıklarına göre yeniden normalize ediyor.
Bunun neden önemli olduğu: "hiç yanıt yok" ile "yanıt geldi ama cap'i aştı" (`0` puan) farklı
iddialar — ilkini `0` olarak puanlamak "en kötü ihtimalde bile en azından cap içinde yanıt verdi"
gibi yanlış bir sinyal verirdi.

### Ağırlıklar: config-driven, varsayılan eşit (1/1/1) — DECIDED

Spec §14 somut bir formül vermiyor. `ResponsivenessWeight`/`ResponseTimeWeight`/`ClosureRateWeight`
(hepsi varsayılan `1.0`) `CompanyIntelligenceOptions`'a eklendi — Sprint 4/7/10'daki "hard-code yok"
paterni. Ağırlıkların toplamının 1'e eşit olması şart değil; `CalculateCandidateExperienceScore`
kullanılan ağırlıkların toplamına bölerek normalize ediyor, bu yüzden kesirli varsayılanlar (0.333…)
yerine tam sayı `1.0`'lar tercih edildi — okunabilirlik için, matematiksel bir fark yaratmıyor.
Unit testte 3x ağırlık verilerek hiçbir eşit-bölme varsayımının hard-code edilmediği doğrulandı.

---

# Spec dokümanındaki küçük tutarsızlıklar (bilgi amaçlı, aksiyon gerektirmiyor)

- Bölüm numaralandırması §32'den sonra §35, sonra §34, sonra §36 şeklinde
  karışık — muhtemelen yazım sırasında sıralama değişmiş.
- §30 sıralaması GDPR'ı KVKK'dan önce listeliyor; Türkiye-first
  pozisyonlamayla tutarlı olması için KVKK önce değerlendirilebilir (hukuki
  görüş gerektirir, bu doküman hukuki tavsiye değildir).
