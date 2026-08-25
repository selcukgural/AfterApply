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

## 5. Cloud provider — OPEN (deployment aşamasına kadar erteleniyor)

Spec'in kendi notu da bunu MVP öncesi zorunlu kılmıyor. Öneri, Sprint 7'de
netleşecek: .NET-ağırlıklı stack için Azure (App Service/Container Apps,
Azure Database for PostgreSQL) doğal entegrasyon sağlıyor; AWS'de daha çok
manuel kablolama gerekir. Kullanıcının mevcut altyapı/deneyimi varsa o
öncelikli olmalı.

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

## Spec dokümanındaki küçük tutarsızlıklar (bilgi amaçlı, aksiyon gerektirmiyor)

- Bölüm numaralandırması §32'den sonra §35, sonra §34, sonra §36 şeklinde
  karışık — muhtemelen yazım sırasında sıralama değişmiş.
- §30 sıralaması GDPR'ı KVKK'dan önce listeliyor; Türkiye-first
  pozisyonlamayla tutarlı olması için KVKK önce değerlendirilebilir (hukuki
  görüş gerektirir, bu doküman hukuki tavsiye değildir).
