# Product & Technical Decisions

Spec kuralı §31.18 gereği: belirsiz kararlar varsayım yapılmadan burada
önerilir ve kullanıcı onayına bırakılır. Bu dosya `ekariyerim-intelligence-platform-plan.md`
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

### Güncelleme (2026-08-26): Postgres + Redis + web de Google Cloud'a taşındı — DECIDED

Kullanıcı Google Cloud'da 90 günlük/$300 kredili bir ücretsiz deneme hesabı
açtı ve tüm parçaların (Postgres, Redis, API, web) tek sağlayıcıda
(Google Cloud) çalışmasını istedi — dört ayrı sağlayıcıya (Neon/Upstash/
Vercel/Cloud Run) bölünmüş yukarıdaki plan yerine:

- **Web (Next.js):** Vercel yerine **ikinci bir Cloud Run servisi**
  (`web/Dockerfile`, bu sprint içinde zaten build-arg'lara göre
  düzeltilmişti — değişiklik gerekmiyor).
- **Postgres:** Neon yerine **Cloud SQL for PostgreSQL**. Cloud Run'ın
  entegre Cloud SQL bağlantısı kullanılıyor (ayrı bir proxy sidecar
  gerekmiyor) — Unix socket üzerinden `Host=/cloudsql/PROJECT:REGION:
  INSTANCE` (Npgsql, `SSL Mode=Disable` — bağlantı zaten proxy tarafından
  şifreleniyor, bu bir güvenlik geriletmesi değil). Runtime servis
  hesabına `roles/cloudsql.client` gerekiyor.
- **Redis:** Upstash yerine **Memorystore for Redis** (Basic tier).
  Memorystore'un public IP'si yok — Google'ın güncel önerisi **Direct
  VPC Egress** (`--network=default --subnet=default`), eski Serverless
  VPC Access connector'a göre daha az kurulum gerektiriyor; projenin
  varsayılan VPC/subnet'i (zaten /20+) Memorystore'un /29+ asgari
  gereksinimini karşılıyor, ayrı bir VPC kurmaya gerek yok. Peering
  Basic/Standard tier'da otomatik ("direct peering mode").

**Maliyet trade-off'u — kullanıcıya açıkça belirtildi, sessizce
yutulmadı:** Cloud Run'ın aksine (kalıcı gerçek ücretsiz katman),
**Cloud SQL ve Memorystore'un hiçbiri her-zaman-ücretsiz bir katmana
sahip değil** — ikisi de sadece 90 günlük/$300 deneme kredisiyle ücretsiz.
Güncel GCP fiyatlandırmasına göre: Cloud SQL'in en küçük kullanılabilir
instance'ı (`db-f1-micro`, Enterprise edition) ≈ $8-10/ay + depolama,
yani deneme bitince ~$10-15/ay; Memorystore Basic tier'ın en küçük
gerçekçi boyutu (1 GiB) ≈ $35-40/ay — üstelik bu, kod tabanında şu an
health check dışında hiçbir yerde kullanılmayan bir Redis instance'ı
için. Kullanıcı bu bilgiyle birlikte GCP'de konsolide etme kararını
onayladı; 90 gün dolmadan önce küçültme/silme ya da Neon/Upstash'e geri
dönme seçenekleri açık bırakıldı, şimdi karara bağlanmadı.

### Redis — DECIDED (2026-08-26, yukarıdaki güncellemeyle Upstash → Memorystore)

Sprint 13 planlaması sırasında bulgu: kod tabanında Redis şu an hiçbir iş
mantığı tarafından kullanılmıyor — `RateLimiting.cs`'teki policy'ler
`RateLimitPartition.GetFixedWindowLimiter` ile in-memory çalışıyor, sadece
`AddHealthChecks().AddRedis(...)` Redis'e bağlı (`DependencyInjection.cs`).
Yani spec'in "Redis where justified" notu bugüne kadar hiç tetiklenmemiş.
Buna rağmen kullanıcı bir Redis servisi eklenmesine karar verdi — ileride
cache/distributed rate-limiting ihtiyacı çıkarsa altyapı hazır olsun diye.
**Servis seçimi güncellendi:** ilk kararda Upstash (kalıcı ücretsiz)
seçilmişti; yukarıdaki "Postgres + Redis + web de Google Cloud'a taşındı"
güncellemesiyle Memorystore'a geçildi — bu artık ücretsiz değil (~$35-40/ay,
sadece deneme kredisiyle geçici olarak ücretsiz), YAGNI açısından bilinçli
kabul edilen bir maliyet, gizlenmiyor.

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

### Sprint 13 kararları ve bulguları (deployment kod/CI hazırlığı, 2026-08-26)

Kullanıcı hesap oluşturmayı (Vercel/GCP/Neon/Upstash/Sentry) kendi başına
paralelde yürütmeyi tercih etti; bu oturumda sadece kod/CI tarafı
hazırlandı — hiçbir gerçek bulut hesabı bu oturumda oluşturulmadı.

- **Sentry, `Sentry:Dsn`/`NEXT_PUBLIC_SENTRY_DSN` boşsa sessizce devre
  dışı — DECIDED.** OpenAI/GoogleOAuth'un `REPLACE_WITH_...` + `StartsWith`
  kontrolü paterni burada kasıtlı olarak *kullanılmadı* — Sentry SDK'sının
  kendisi zaten boş bir Dsn'i "devre dışı" olarak yorumluyor (event
  göndermiyor, hata fırlatmıyor); sahte-ama-dolu bir string tam tersine her
  istekte başarısız bir gönderim denemesine yol açardı. Bu yüzden
  `appsettings.json`'da `Sentry:Dsn` boş string (`""`), placeholder değil.
- **Backend: `Sentry.AspNetCore` 6.9.0, `Program.cs`'te `builder.WebHost.
  UseSentry(...)` en başta — DECIDED.** Serilog'dan önce çağrılıyor (Sentry
  .NET SDK'sının kendi önerisi: başlangıç hatalarını da yakalayabilmesi
  için mümkün olduğunca erken). `Sentry.Serilog` (log-event forwarding)
  bilinçli olarak eklenmedi — `UseSentry` zaten yakalanmamış exception'ları
  middleware üzerinden yakalıyor, MVP için yeterli; iki paralel mekanizma
  (middleware + log sink) bu aşamada gereksiz karmaşıklık olurdu.
- **Frontend: `@sentry/nextjs` 10.71.0, Next.js 16 App Router'ın güncel
  dosya kuralıyla — DECIDED.** `web/AGENTS.md`'nin uyarısı ciddiye alındı
  (bu Next.js sürümü training-data'dan farklı olabilir) —
  `node_modules/next/dist/docs/`'taki güncel `instrumentation.md`/
  `instrumentation-client.md` doğrudan okunup ona göre yazıldı:
  `sentry.client.config.ts` DEĞİL, `src/instrumentation-client.ts` (Next
  15.3+'ta değişen konvansiyon) + `src/instrumentation.ts`'te `register()`/
  `onRequestError` + `Sentry.captureRequestError`. `next.config.ts`
  `withSentryConfig` ile sarıldı; `org`/`project`/`authToken` env
  değişkenlerinden okunuyor, hiçbiri yokken build'in kırılmadığı
  (getsentry/sentry-javascript'te doğrulanmış davranış: source map upload
  sessizce atlanıyor, sadece bir notice basılıyor) doğrulandı —
  `npm run build` gerçekten hatasız tamamlandı.
- **`disableLogger` next.config seçeneği eklenmedi — DECIDED.** İlk
  denemede eklenmişti, build "deprecated, Turbopack'te desteklenmiyor"
  uyarısı verdi (proje sadece Turbopack kullanıyor) — kaldırıldı.
  `onRouterTransitionStart = Sentry.captureRouterTransitionStart` ise
  build'in "ACTION REQUIRED" uyarısı üzerine eklendi, ikinci build'de her
  iki uyarı da temiz çıktı.
- **Bulgu: `NEXT_PUBLIC_API_BASE_URL` prod profilinde hiç işlemiyordu —
  düzeltildi.** `docker-compose.prod.yml`, bu değişkeni `web` servisine
  `environment:` (container **runtime**'ı) olarak veriyordu, ama Next.js
  `NEXT_PUBLIC_*` değişkenlerini `next build` **anında** (image build
  stage'i) client bundle'a gömüyor — `web/Dockerfile` bu değişkeni hiç bir
  build `ARG` olarak tanımlamıyordu, yani tarayıcı tarafı kod her zaman
  `undefined` görüyordu. Sprint 13'ün kendi `NEXT_PUBLIC_SENTRY_DSN`'ini
  eklerken fark edildi (aynı mekanizma). Düzeltme: `web/Dockerfile`'a iki
  `ARG`/`ENV` çifti eklendi, `docker-compose.prod.yml`'de `web.environment`
  yerine `web.build.args` kullanılıyor artık. `podman build` ile hem
  `api` hem `web` image'ları bu değişiklikle yeniden doğrulandı.
- **CI/CD: Cloud Run deploy'u Workload Identity Federation ile, statik
  JSON key yok — DECIDED.** `google-github-actions/auth@v3` +
  `deploy-cloudrun@v3`; GitHub Secrets'ta sadece proje id/region/WIF
  provider/service account adı tutuluyor, uzun ömürlü bir credential
  tutulmuyor. `.github/workflows/deploy-backend.yml` bilinçli olarak
  `workflow_dispatch`-only (gerçek GCP kaynakları henüz yok, `push: main`
  her commit'te kırmızı X üretirdi) — `push` tetikleyicisi dosyada yorumlu
  halde duruyor, DEPLOYMENT.md'nin son adımı bunu ne zaman açacağını
  anlatıyor.
- **Vercel için ayrı bir GitHub Actions workflow'u YOK — DECIDED.**
  Vercel'in kendi Git entegrasyonu (dashboard'dan repo bağlama) push'ta
  otomatik build+deploy yapıyor — bunu tekrar eden bir custom workflow
  yazmak gereksiz karmaşıklık olurdu (YAGNI).
- **Secret Manager'a taşınan değerler, backend'in mevcut `REPLACE_WITH_...`
  placeholder'larını da içeriyor (Gmail OAuth) — DECIDED.** Boşken zararsız
  olsalar da tutarlılık için (hepsi aynı mekanizmadan okunsun) Secret
  Manager'a konuluyor; gerçek değerler sadece o entegrasyon kurulunca
  girilecek.

### Sprint 13 — gerçek deploy (2026-08-26): `ekariyerim` projesi, bulgular ve düzeltmeler

Yukarıdaki kod/CI hazırlığı, kullanıcıyla birlikte gerçek bir GCP projesinde
(`ekariyerim`, region `europe-west1`) uçtan uca uygulandı. Backend
(`afterapply-api`), frontend (`afterapply-web`), Cloud SQL, Memorystore hepsi
ayağa kalktı; `https://ekariyerim.com` (Cloudflare'den alınmış) domain
mapping'i kuruldu. Bu süreçte planın öngörmediği 4 gerçek sorun bulundu:

1. **Cloud Run varsayılan olarak private — `deploy.yml` bunu hiç
   ayarlamıyordu — bulgu + DECIDED (elle, CI'a gömülmeden).** İlk deploy'dan
   sonra `/health`'e istek atınca 403 (Google'ın "Forbidden" sayfası)
   döndü — imaj/secret/kod sorunu değil, servisin `allUsers` için
   `roles/run.invoker` izni hiç yoktu. `google-github-actions/deploy-cloudrun`
   dokümantasyonu bunu doğruluyor: yeni servisler varsayılan private, ve
   Google'ın kendi önerisi CI/CD'nin bu ayarı yönetmemesi ("a Cloud Run
   product recommendation is that CI/CD systems not set or change settings
   for allowing unauthenticated invocations") — bu yüzden `deploy.yml`'e
   `--allow-unauthenticated` eklenmedi, bunun yerine bir kerelik elle:
   `gcloud run services add-iam-policy-binding <servis> --member=allUsers
   --role=roles/run.invoker` (hem `afterapply-api` hem `afterapply-web`
   için). DEPLOYMENT.md'ye "6. Servisleri herkese açın (tek seferlik)"
   adımı olarak eklendi.
2. **Migration'lar planın bir parçasıydı ama unutulması kolay bir
   adımdı — bulgu.** İlk deploy sonrası kayıt olma denemesi 500 döndü
   (Postgres şemasız — `AspNetUsers` tablosu yok). DEPLOYMENT.md'nin
   "Migrations" adımı zaten vardı ama akışta "deploy bitti, artık
   çalışıyor" hissi migration adımının atlanmasına yol açtı — DoD'ye
   "kayıt ol → 201 dönüyor" gibi somut bir uçtan-uca kontrol eklenmesi
   gerektiği görüldü.
3. **Migration bağlantı yöntemi: Cloud SQL Auth Proxy değil, geçici
   authorized-networks — DECIDED (gerçekte kullanılan, dokümantasyon
   güncellendi).** DEPLOYMENT.md'nin önerdiği Cloud SQL Auth Proxy yöntemi
   yerel `gcloud` kurulumu/ADC gerektiriyordu (bu makinede yoktu). Bunun
   yerine: kullanıcının güncel public IP'si `gcloud sql instances patch
   --authorized-networks=<ip>/32` ile geçici izinli hale getirildi, Cloud
   SQL'in kendi public IP'sine `SSL Mode=Require` ile doğrudan bağlanıldı,
   migration sonrası `--clear-authorized-networks` ile erişim kapatıldı.
   Daha az yeni araç kurulumu gerektirdiği için DEPLOYMENT.md'nin birincil
   yöntemi bu oldu, proxy alternatif olarak kaldı.
4. **Secret Manager'da uzun tek-satırlık `printf | gcloud secrets create`
   komutları, kopyala-yapıştır sırasında bozulabiliyor — bulgu +
   DECIDED.** `afterapply-sentry-dsn` secret'ı "kap" olarak oluştu ama 0
   version'la kaldı (`gcloud secrets versions list` ile doğrulandı) —
   kullanıcının kopyalama akışı (chat'ten seçip harici bir editöre, oradan
   Cloud Shell'e) uzun satırları görünmez şekilde bozuyordu. İki kalıcı
   düzeltme: (a) runbook artifact'ine her kod bloğu için gerçek bir
   "Kopyala" butonu eklendi (`navigator.clipboard.writeText`, tarayıcının
   görsel satır sarmalamasından etkilenmeyen tam metni kopyalıyor,
   `document.execCommand` fallback'i var), (b) Secret Manager adımındaki
   komutlar `printf | gcloud secrets create` yerine `cat <<EOF > /tmp/dosya`
   + `--data-file=/tmp/dosya` paternine geçirildi — hem daha kısa satırlar
   hem `cat /tmp/dosya` ile içeriği yazmadan önce/sonra doğrulama imkanı.
   Bozulan tek secret, Secret Manager Console'un kendi "+ NEW VERSION" form
   alanından (hiç terminal kullanmadan) düzeltildi — bu, personalize
   edilmesi gereken tekil değerler için artık önerilen yol.

**Sonuç (2026-08-26):** `https://afterapply-api-*.run.app/health` → 200
Healthy (Postgres+Redis), kayıt akışı → 201 + JWT, `https://ekariyerim.com`
domain mapping'i kuruldu (SSL provisioning bekleniyor, `DomainRoutable: True`
doğrulandı). Sprint 13 DoD'si fiilen karşılandı.

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

### Sprint 13 gerçek deploy'unda bulunan iki extension bug'ı — DÜZELTİLDİ (2026-08-26)

Extension, `ekariyerim` prod ortamına karşı ilk kez gerçek bir kullanıcı tarafından test edildi;
iki gerçek bug bulundu:

- **`manifest.json`'ın `host_permissions`'ı prod API origin'ini içermiyordu.** Sprint 9'un kendi
  notu bunu zaten öngörmüştü ("gerçek bir prod API origin'i devreye girdiğinde bu listeye ayrıca
  eklenmesi gerekecek") ama unutulmuştu — `["https://www.linkedin.com/*", "http://localhost/*"]`
  listesinde `https://afterapply-api-*.run.app` yoktu, Manifest V3 bu yüzden extension'ın fetch
  çağrısını sessizce engelliyordu (hata sayfanın değil, extension'ın kendi console'unda görünüyor
  — kullanıcı ilk başta hatayı hiç göremedi). Düzeltme: hem güncel Cloud Run URL'i hem gelecekteki
  `https://api.ekariyerim.com/*` listeye eklendi (domain SSL'i hazır olunca extension'ı tekrar
  güncellemeye gerek kalmasın diye).
- **Location scraping, sabit hop-sayılı DOM-yürüyüşü yüzünden bu spesifik sayfada hep boş
  dönüyordu — DÜZELTİLDİ.** Gerçek bir LinkedIn arama sonucu sayfasında (claude-in-chrome ile
  canlı DOM incelenerek) doğrulandı: aynı `/jobs/search-results/` sayfasında bile, promosyonlu bir
  ilan (`Turknet`) ile promosyonsuz bir ilan (`Figensoft`) arasında konum satırının başlığın
  `<p>`'sine göre derinliği **farklıydı** (2 seviye vs. 3 seviye yukarı) — Sprint 9'un sabit
  "`.parentElement.parentElement.nextElementSibling.nextElementSibling`" yürüyüşü ikisinde de
  yanlış elemente düşüyordu (boş bir div ya da "Apply/Saved" butonları). Yeni yaklaşım: başlığın
  `<p>`'sinden yukarı doğru (en fazla 6 seviye) her ata seviyesinde, o atanın **doğrudan alt
  elemanları** arasında başlık paragrafı olmayan ve `·` (LinkedIn'in metadata ayracı) içeren bir
  `<p>` arıyor — bulunca `·`'den önceki kısmı (konum) alıyor. Hop-sayısından bağımsız olduğu için
  her iki ilan tipinde de doğrulandı. Konum hâlâ en az kritik alan (boş kalırsa elle girilir),
  bu bir hard-fail değil.

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

## Sprint 14 kararları ve bulguları (Public landing page)

Kaynak: kullanıcının eklediği `LANDING_PAGE_SPEC.md` (43 bölüm). O dosya §41 gereği
implementasyondan önce repo assessment + component plan + design plan + phased plan + open
decisions çıktısı isteniyor — bu bölüm o çıktının kalıcı kaydı.

### `/` artık dashboard değil, public landing page — DECIDED

`(protected)/page.tsx` (dashboard) `/` route'unu tutuyordu; public bir landing sayfası aynı
route'u paylaşamaz. Dashboard `(protected)/dashboard/page.tsx`'e taşındı. `NavBar` (uygulama
içi), login/register sonrası yönlendirme ve `(public)/layout.tsx`'teki logo linki `/dashboard`'a
güncellendi.

### Landing page `(public)` route group'unun dışında, kendi route'u — DECIDED

`(public)/layout.tsx` login/register/privacy için minimal bir header (logo + dil/tema) render
ediyor, `max-w-5xl` `<main>` içinde. Landing page'in spec §6'daki kendi navbar'ı (logo, anchor nav,
Sign in, Get started, mobil hamburger) var — bu yüzden `(public)` grubuna değil, `web/src/app/
[locale]/page.tsx` olarak grupların dışına, kendi tam genişlikli layout'uyla eklendi.

### Roadmap iki katmanlı (TODAY/FUTURE), spec'in önerdiği üç katmanlı (TODAY/NEXT/FUTURE) değil — DECIDED

Spec §37 "AI matching, Gmail entegrasyonu, browser extension MVP'de yok, future" varsayıyor —
ama repo'da üçü de gerçek: `Application.Matching` + settings.cv (AI match), `Application.
EmailIntegrations` + `/settings/email-suggestions` (Gmail), `extension/` klasöründeki gerçek MV3
eklentisi + settings.extension token akışı (browser extension). Spec'in kendi §37 kuralı zaten
"repo implement ettiğini gösteriyorsa future değil" diyor, o yüzden bu üçü roadmap'te "Bugün"
altında, "Gelecek" boş bırakılmadı — sadece "Anonim, toplu işe alım içgörüleri" (bkz. aşağıki
madde) orada kaldı.

### "Daha büyük bir şey inşa ediyoruz" (Vision) bölümü gerçekten future olarak bırakıldı — DECIDED

`CompanyIntelligence` modülü (`CandidateExperienceScore`, `ClosureRate`,
`/api/company-intelligence/{id}`) sunucu tarafında var ama feature-flag ile kapalı, auth
gerektiriyor, tek şirket bazlı (anonim/toplu endpoint yok) ve frontend'de hiç kullanılmıyor
(`grep companyIntelligence web/src` boş döndü). Spec'in bu bölümü "henüz yok, vizyon" olarak
sunma talimatı burada doğru — landing page'de açık bir "bu henüz mevcut değil" notu ile
(`landing.vision.disclaimer`) verildi, mevcut bir özellikmiş gibi sunulmadı.

### Mock görsellerde gerçek dashboard bileşenleri reuse edildi — DECIDED

`StatTile`, `ResponseTimeCard`, `StatusDistributionChart` (`web/src/components/dashboard/`) saf
prop-driven bileşenler, içeride fetch yok — bu yüzden landing page'in hero/analytics mock'larında
gerçek API çağrısı yapmadan, sabit örnek veriyle doğrudan reuse edildi (spec §34: "reusable
landing-page visual, manuel yeniden yaratmaya tercih edilir"). Her mock görselin yanında "Örnek
veri" / "Sample data" rozeti var (spec §38: mock ile gerçek veri ayrımı net olmalı).

### `sitemap.ts` eklendi, domain netleşti — DECIDED (Sprint 14'te OPEN bırakılmıştı)

Public domain **https://ekariyerim.com** olarak teyit edildi (Sprint 15). `web/src/app/sitemap.ts`
(`/`, `/login`, `/register`, `/privacy` × `tr`/`en`), `[locale]/layout.tsx`'te `metadataBase`, ve
landing `page.tsx`'te `alternates.canonical`/`languages` eklendi; `robots.ts`'e `sitemap:` alanı
eklendi.

---

## Sprint 15 kararları ve bulguları (ekariyerim rebrand + logo)

### Rebrand kapsamı: sadece kullanıcıya görünen metinler — DECIDED

Domain `https://ekariyerim.com` netleşince marka adı "AfterApply"dan "e-kariyerim"e çevrildi —
ama yalnızca UI metinlerinde (web app: navbar/footer/sayfa başlıkları/gizlilik metni/e-posta,
tarayıcı eklentisi: manifest/popup/options metinleri). `.NET` proje/namespace isimleri
(`AfterApply.Api` vb.), `.slnx`, GitHub repo adı ve `extension/manifest.json`'daki Cloud Run
host_permission URL'si kasıtlı olarak değiştirilmedi — bu, `DECISIONS.md` #0'da zaten verilmiş
"iç kod ismi `AfterApply.*` kalır" kararıyla tutarlı; kullanıcı bu kapsamı onayladı.

### Görünen marka adı "e-kariyerim" (tireli), domain/e-posta "ekariyerim" (tiresiz) — DECIDED

Kullanıcı tireyi tercih etti: Türkçede "e-devlet/e-fatura/e-ticaret" kalıbıyla örtüşüyor, "e-"
(elektronik/online) ön ekini anında okutuyor. Domain (`ekariyerim.com`) ve e-posta
(`privacy@ekariyerim.com`) tiresiz kaldı — marka adı ile domain adının farklı olması yaygın bir
pratik, teknik bir kısıt değil. `web/src/app/[locale]/(public)/privacy/page.tsx`'teki mailto ve
`extension/manifest.json`'daki `api.ekariyerim.com` host_permission'ı bu yüzden dokunulmadı.

### Logo: AI görsel üretme aracı yok, `design` skill ile vektör mark tasarlandı — DECIDED

Kullanıcı başta "üretilmiş görsel logo (PNG/AI görsel)" istedi; bu ortamda fotoğrafik/illustratif
görsel üreten bir araç olmadığı belirtildi ve alternatif olarak `design` skill'iyle vektör bir
logo işareti tasarlanması teklif edildi, kullanıcı onayladı. Üç konsept (konuşma balonu+onay,
zarf+rozet, belge+rozet) bir Claude Design canvas'ında sunuldu; kullanıcı Konsept A'yı (konuşma
balonu içinde onay işareti — "başvurdun, gerçek bir cevap aldın") seçti.

### Tek SVG kaynağından PNG üretimi: `rsvg-convert` — DECIDED

Bu makinede `rsvg-convert` (Homebrew) kurulu bulundu, yeni bir proje bağımlılığı eklenmedi.
`web/src/app/icon.svg` (şeffaf arka plan, tarayıcı favicon'u — Next.js dosya konvansiyonu) ve
`web/src/app/apple-icon.png` + `extension/icons/icon{16,48,128}.png` (beyaz yuvarlak-köşe arka
planlı "badge" varyantı, opak arka plan gerektiren bağlamlar için) aynı ikon path'lerinden
türetildi. Eski jenerik `web/src/app/favicon.ico` silindi — `icon.svg` onun yerini alıyor.

### `Logo` bileşeni `currentColor` ile tema-uyumlu — DECIDED

`web/src/components/layout/Logo.tsx` ikon `stroke="currentColor"` + sarmalayıcı `text-blue-600
dark:text-blue-400` kullanıyor — ayrı açık/koyu SVG dosyası gerekmiyor. `(public)/layout.tsx`,
`LandingNavbar.tsx`, `LandingFooter.tsx`, `NavBar.tsx`'teki düz metin "AfterApply" wordmark'ları
bu bileşenle değiştirildi.

---

## Deploy pipeline'ına otomatik migration adımı (2026-08-27)

### Migration'lar artık deploy.yml'de otomatik, ayrı bir Cloud Run Job üzerinden — DECIDED

Sprint 13'te bilinçli olarak "migration'lar hep elle, `dotnet ef database update`" kararı
verilmişti (DEPLOYMENT.md "Migrations"). `AddCompanyNameTrigramIndex` migration'ının prod'a elle
uygulanması sırasında kullanıcı bunun her deploy'da otomatikleşmesini istedi — ama DB parolasının
bir AI asistanına (veya GitHub Actions loglarına) hiç geçmemesi şartıyla.

Seçilen yaklaşım: `dotnet ef migrations bundle` ile self-contained bir `efbundle` executable
üretilip (`src/AfterApply.Api/Dockerfile.migrate`) `afterapply-migrate` adında ayrı bir **Cloud
Run Job** olarak deploy ediliyor. `deploy.yml`'in `deploy-backend` job'ı, `afterapply-api`
servisini güncellemeden **önce** bu job'ı build edip `gcloud run jobs execute --wait` ile
çalıştırıyor — job kendi runtime service account'u üzerinden `afterapply-postgres-connection`
secret'ını doğrudan Secret Manager'dan okuyor (API servisinin zaten yaptığı gibi), yani parola
CI/CD pipeline'ına hiç girmiyor. `dotnet ef database update` idempotent olduğu için (sadece
`__EFMigrationsHistory`de eksik olan migration'ları uygular) her deploy'da çalıştırmak güvenli —
uygulanacak bir şey yoksa no-op.

Reddedilen alternatif: CI runner'ında Cloud SQL Auth Proxy başlatıp `dotnet ef` çalıştırmak —
daha az altyapı değişikliği gerektiriyordu ama DB parolasının GitHub Actions secret'ı olarak
saklanıp CI runner'ının belleğinden geçmesini gerektiriyordu; kullanıcı Cloud Run Job'ı tercih
etti çünkü parola hiç GCP dışına çıkmıyor.

Program.cs'in container başlangıcında otomatik `Database.Migrate()` çağırmama kararına
dokunulmadı — migration hâlâ ayrı, explicit bir adım, sadece artık elle değil deploy pipeline'ı
tarafından tetikleniyor.

Yan not: `AppDbContextFactory`'e `.AddEnvironmentVariables()` eklendi — `migrations bundle`
komutu CI/Docker build'inde user-secrets olmadan çalışabilsin diye (bundle gerçek bir DB'ye
bağlanmıyor, sadece modeli okumak için `ConnectionStrings:Postgres`'in "configured" olmasını
istiyor; build sırasında placeholder bir değer veriliyor, gerçek değer job çalışırken Secret
Manager'dan geliyor).

---

## AI Job Matching (Sprint 8) — kullanıcıdan gizlendi (2026-08-29)

**Karar:** `Matching:Enabled` config flag'i eklendi (varsayılan `false`,
`CompanyIntelligence:Enabled` paterninin birebir tekrarı). `/api/matching/*`
altındaki 4 endpoint (`MatchingEndpoints.cs`) artık flag kapalıyken
grup-seviyesi bir `AddEndpointFilter` ile her çağrıda `404 NotFound`
dönüyor — flag'in varlığı dışarıdan ayırt edilemiyor (CompanyIntelligence
DoD'siyle aynı prensip). Frontend'de iki giriş noktası tamamen kaldırıldı
(render edilmiyor, ilgili state/effect/handler'larla birlikte silindi):
`settings/page.tsx`'teki CV metni bölümü ve `applications/[id]/page.tsx`'teki
`JobMatchPanel`. Kod silinmedi (`JobMatchPanel.tsx`, `lib/api/matching.ts`,
`CandidateProfile`/`JobMatch` domain kodu duruyor), sadece erişilemez.

**Gerekçe:** `PRIVACY_CHECKLIST.md`'nin "Avukata götürülecek envanter ve
eksikler" bölümünde en kritik madde olarak işaretlendi — bu faz kullanıcının
CV metnini ham hâlde OpenAI'a (ABD, yurt dışı) gönderiyor, ama ne granüler
bir açık rıza ne de yurt dışı aktarım disclosure'ı var. Kullanıcının bir
avukatı yok; gerçek kullanıcı hacmi düşükken bu riski taşımak yerine özelliği
kullanıcıdan tamamen gizlemek tercih edildi. Yeniden açılması, gerekli
KVKK metinleri (Aydınlatma Metni + açık rıza + yurt dışı aktarım
disclosure'ı) hazırlanana kadar ertelendi — bkz. DEVELOPMENT_PLAN.md
Sprint 8 notu.

**Not:** CompanyIntelligence'ın aksine burada frontend'de UI hiç
"flag'e göre koşullu render" olarak yazılmadı — doğrudan kod render
ağacından çıkarıldı. Sebep: mevcut kod tabanında backend flag'ini frontend'e
taşıyan bir mekanizma hiç yoktu (CompanyIntelligence de zaten hiç UI'a sahip
değildi, bu yüzden örnek teşkil etmiyordu); böyle bir mekanizma kurmak bu
tek-seferlik gizleme işi için orantısız olurdu (YAGNI) — flag geri
açıldığında bu iki JSX bloğu + state'i geri eklemek, DECISIONS.md'nin bu
notuyla birlikte yeterli.

---

## Gmail Integration (Phase 9) — kullanıcıdan gizlendi (2026-08-29)

**Karar:** `EmailIntegrations:Enabled` config flag'i eklendi (varsayılan
`false`, `Matching:Enabled`/`CompanyIntelligence:Enabled` paterninin birebir
tekrarı). `/api/email-integrations/*` altındaki tüm route'lar (6 route,
`/gmail/callback` dahil — o route anonymous ama aynı `MapGroup` altında
olduğu için filtre onu da kapsıyor) `EmailIntegrationEndpoints.cs`'teki
grup-seviyesi bir `AddEndpointFilter` ile flag kapalıyken her çağrıda `404
NotFound` dönüyor. Ayrıca `Program.cs`'teki `gmail-sync` Hangfire recurring
job'ı da flag kapalıyken artık hiç register edilmiyor — mevcut
(disconnect edilmemiş) bağlantılar olsa bile arka planda Gmail API'ye
sync çağrısı yapılmıyor. Frontend'de `settings/page.tsx`'teki Gmail kartı
(bağlan/bağlantıyı kes/öneri listesine link) ve ilgili
state/effect/handler'lar tamamen render ağacından çıkarıldı — Matching'in
aynı YAGNI kararının tekrarı, `emailIntegrationsApi` client'ı ve
`settings/email-suggestions/page.tsx` route'u kod olarak duruyor, sadece
erişilemez hâle geldi.

**Gerekçe:** Uygulama canlıya alındıktan sonra Gmail entegrasyonunun gerçek
kullanıcılara açılabilmesi için OAuth consent screen'in "In production"a
geçmesi gerektiği, bunun da `gmail.readonly`'nin restricted-scope olması
sebebiyle Google'ın CASA güvenlik değerlendirmesini (üçüncü-taraf assessor,
~$15.000-$75.000, 4-12+ hafta) gerektirdiği ortaya çıktı — bkz.
`PRIVACY_CHECKLIST.md` madde 7. Bu yatırım kararı henüz verilmedi (ayrıca
konuşulacak); o karara kadar özelliği "Testing" modunda yarı-açık/yanlışlıkla
erişilebilir bırakmak yerine (100 test user sınırı + "doğrulanmamış uygulama"
uyarısı zaten genel kullanıcıya uygun değildi) tamamen gizlemek tercih
edildi. Testler güncellendi: mevcut `EmailIntegrationTests.cs`
`EmailIntegrations:Enabled=true` ile flag'i açık tutuyor (fonksiyonel
testler bozulmadı), ayrıca CompanyIntelligence'ın iki-factory desenini
tekrarlayan 4 yeni test flag kapalıyken `/connect`, `/status`,
`/suggestions`, `/callback`'in 404 döndüğünü doğruluyor.

---

## e-kariyerim rebrand'inin backend/dış-servis genişletmesi (2026-08-29)

**Karar:** Sprint 15'teki "sadece kullanıcıya görünen metinler" kapsamı (bkz. §Sprint 15 —
"Rebrand kapsamı: sadece kullanıcıya görünen metinler") o zaman web app + extension UI metinlerini
kapsamıştı; backend'in dış servislere (LinkedIn, kariyer.net) giden User-Agent header'ı gibi
kod-içi ama dış-görünür stringler gözden kaçmıştı. Bugün ek olarak değiştirildi:
`JobLinkPreviewService.cs`/`CompanyEnrichmentService.cs`'teki User-Agent
(`"AfterApplyLinkPreview/1.0 (+https://afterapply.app)"` → `"EKariyerimLinkPreview/1.0
(+https://ekariyerim.com)"`), `ImportService.cs`'teki temp-dizin öneki, `postman/`'daki koleksiyon/
environment display-name'leri, kök dizin dokümanlarının (`README.md`, `DEVELOPMENT_PLAN.md`,
`extension/README.md`) başlık/prose kısımları, ve spec dosyasının adı
(`afterapply-intelligence-platform-plan.md` → `ekariyerim-intelligence-platform-plan.md`, git mv).

Sprint 15'in #0 kararıyla tutarlı olarak **değiştirilmedi**: `.NET` proje/namespace isimleri
(`AfterApply.Api` vb.), `.slnx`, GitHub repo adı, `.github/workflows/*.yml` ve
`docker-compose.yml`/`.env*.example`'daki gerçek GCP/Cloud Run/Cloud SQL/Postgres kaynak adları
(`afterapply-api`, `afterapply-db`, `afterapply-*` secret'ları vb. — bunlar gerçek deploy edilmiş
altyapıyı işaret ediyor, yeniden adlandırmak ayrı bir altyapı migrasyonu gerektirir),
`extension/manifest.json`'daki eski Cloud Run host_permission'ı, ve `extension/storage.js`'teki
`chrome.storage` anahtar stringleri (`afterapply_settings`/`afterapply_theme` — dahili, kullanıcıya
hiç görünmeyen anahtarlar; değiştirmek zaten kurulu extension'ın token/tema ayarını sıfırlanmış
gibi gösterirdi). Kullanıcı bu kapsamı (ve spec dosyasının yeniden adlandırılmasını) onayladı.

---

## Testcontainers orphan sızıntısı: manuel "kontrol et" adımı yerine otomatik temizlik (2026-08-29)

**Karar:** `tests/AfterApply.IntegrationTests/TestContainerCleanup.cs` eklendi — test assembly'si
yüklenir yüklenmez (`[ModuleInitializer]`, herhangi bir fixture/container oluşmadan önce)
`TESTCONTAINERS_RYUK_DISABLED=true` iken `org.testcontainers=true` etiketli tüm container'ları
`podman rm -f` ile temizliyor. Gerçek Docker/CI'da (Ryuk çalışırken) no-op.

**Gerekçe:** Ryuk bu makinede rootless podman altında hiç çalışamıyor (§Podman VM undersized
notunda 2026-08-26'da tespit edilmişti — socket'i bind-mount edemiyor). O zamanki çözüm "her
koşumdan önce `podman ps -a`'ya bak, sızıntı varsa temizle" idi — ama bu manuel adım hiçbir
oturumda tutarlı hatırlanmadı; 2026-08-29'da 5 kesintiye uğramış koşumdan kalma 79 container
(bazıları 7+ saattir açık) birikmiş, tek başına normalde ~36sn süren bir koşumu 35+ dakikaya
çıkarmıştı (podman VM'i kaynak açlığından). Simüle edilmiş bir kesintiyle (koşum ortasında
`kill -9`, 28 container sızdırıldı) doğrulandı: bir sonraki koşum bu 28'i otomatik temizledi,
container sayısı koşum sonunda 0'a döndü, süre ~39sn'de sabit kaldı. Bonus: bu sızıntının yan
etkisi olan aralıklı "proxy already running" (podman port-forward çakışması) hatası da bu düzeltmeyle
birlikte bir daha gözlenmedi.

**Not:** Bu düzeltme tek geliştiricili yerel makineyi varsayıyor — iki `dotnet test` çağrısının tam
aynı anda yarışması teorik olarak birbirinin yeni başlattığı container'ı silebilir; kabul edilebilir
bir tradeoff, paylaşılan bir CI runner'ında (gerçek Docker, Ryuk çalışır) zaten no-op olduğu için
sorun teşkil etmiyor.

---

## Integration test suite: seri çalıştırma + Hangfire shutdown timeout (2026-08-29)

**Karar:** İki ek düzeltme daha yapıldı. (1) `tests/AfterApply.IntegrationTests/xunit.runner.json`
eklendi (`maxParallelThreads: 1`) — hiçbir test sınıfının `[Collection]` attribute'u yok, bu yüzden
xUnit'in varsayılanı ~16 sınıfın hepsinin Postgres+Redis container'ını aynı anda ayağa kaldırmasına
izin veriyordu (gözlemlendi: 24 container aynı anda başlatıldı, 6+ dakika hiç ilerleme yok) —
sızıntıdan bağımsız, ayrı bir yavaşlık/instabilite kaynağı. (2)
`AddHangfireServer()`'ın varsayılan 15sn `ShutdownTimeout`'u yük altında yetersizdi — hiçbir job
çalışmıyorken bile Hangfire'ın kendi watchdog/heartbeat thread'lerinin kapanması için
`WaitForShutdownAsync` timeout atıp `TaskCanceledException` fırlatıyordu (test teardown'ında,
`DisposeAsync` içinde) — 30sn'ye çıkarıldı (`DependencyInjection.cs`, `AddBackgroundJobs`).

**Gerekçe:** Sızıntı/volume düzeltmesinden sonra bile tam suite koşumu ara sıra "Test host process
crashed" ile çöküyordu (rastgele bir test sayısında, 14-37 arası). Bellek (RSS <500MB, host 48GB),
podman VM sağlığı (uptime/load normal, hiç restart olmadı) ve disk/volume elendi. Yukarıdaki iki
düzeltmeyle birlikte tam suite artık tekrar tekrar 74/74 tamamlanıyor (~2.5-3dk, seri).

**Çözülmemiş kalan:** Nadiren (post-fix iki temiz koşumda hiç, önceki denemelerde sıkça) test host
yine de rastgele bir noktada çökebiliyor — yönetilen bir exception/stack trace olmadan, gerçek bir
process ölümü. `~/Library/Logs/DiagnosticReports`'ta `dotnet`/`testhost` için hiç crash raporu yok;
`log show` çökme anlarında sıradan XPC/security-exception gürültüsü dışında bir şey göstermiyor.
Native crash dump aracı olmadan kök nedeni bulunamadı — Testcontainers/Docker.DotNet'in rootless
podman socket'i üzerindeki etkileşiminde nadir bir native-seviye kararsızlık gibi görünüyor.
**Bilinçli olarak yapılmadı:** 16 sınıfın her birinin kendi container çiftini tek, paylaşılan bir
`ICollectionFixture`'a indirmek (container lifecycle sayısını ~16x azaltır, muhtemel gerçek çözüm)
— çünkü `CompanyIntelligenceTests` kullanıcılar-arası agregasyonu test etmek için temiz,
tek-kiracılı bir DB'ye ihtiyaç duyuyor; 16 sınıfı tek DB'de paylaştırmak mekanik bir
find-replace değil, her sınıfın izolasyon varsayımlarının tek tek gözden geçirilmesini gerektirir.

---

## E-postadan yeni ilan/başvuru oluşturma (2026-08-31)

`EmailForwardingService.ProcessInboundEmailAsync`, eşleşmeyen bir email için artık her zaman
sessizce dönmüyor — kullanıcıyla netleştirilen 3 karar:

1. **Öneri kuyruğu, doğrudan yazma değil.** `EmailSuggestion.ApplicationId` `Guid?` oldu; `null`
   olan satırlar "yeni ilan önerisi" (yeni `CreateForNewJob` factory'si), non-null olanlar
   bugünkü "mevcut başvurunun statüsünü güncelle" önerisi. `ConfirmSuggestionAsync` onaya kadar
   hiçbir Company/Application yazmıyor — DECISIONS.md'nin "Eşleşmeyen email'ler gösterilmiyor"
   (Phase 9) temkinliliğiyle tutarlı, sadece artık tamamen sessiz kalmak yerine kullanıcıya
   gösterip onay istiyor.
2. **Sadece durum sinyali taşıyan email'ler tetikler.** `EmailApplicationMatcher.Match` `null`
   dönse bile, `RuleBasedEmailClassifier`/`IEmailClassificationProvider` bir sinyal (statü veya
   "StillWaiting") bulamazsa hâlâ hiçbir şey oluşturulmuyor — yeni `IEmailJobExtractionProvider`
   (ayrı bir LLM çağrısı, `OpenAiEmailClassificationProvider`'ın ikizi) sadece sinyal varsa
   devreye giriyor.
3. **Şirket adı veya pozisyon başlığı güvenle çıkarılamazsa öneri yok.** Extraction provider
   `confident: false` veya boş `companyName`/`jobTitle` durumunda `null` döner, çağıran taraf
   sessizce atlar — yarım/hatalı veriyle kayıt açılmıyor.

Bu, yalnızca Forwarding path'i değiştiriyor — Gmail OAuth path'i (`EmailIntegrationService`,
terk edilmiş yön, bkz. proje hafızası) hiç dokunulmadı, ama ikisinin paylaştığı
`GetPendingSuggestionsAsync`/`ConfirmSuggestionAsync`/`/settings/email-suggestions` altyapısı her
iki tip suggestion'ı da işleyecek şekilde güncellendi. Onaylanan bir yeni-ilan önerisi
`IApplicationService.CreateAsync` ile (ek bir method gerekmeden, mevcut `Source.Email` enum
üyesiyle) oluşturuluyor — `Application.Source == Email` zaten var olan ama hiç UI'da
render edilmeyen bir alandı, kullanıcı görsün diye başvuru detay sayfasına bir badge eklendi.

`email-worker/src/index.js`'in `SNIPPET_MAX_LENGTH`'i 300'den 2000'e çıkarıldı (lokasyon/açıklama
genelde email'in ilk 300 karakterinden sonra geliyor) — `EmailSuggestionConfiguration`'ın
`Snippet` sütun uzunluğuyla senkron tutulmalı.

---

## Gmail OAuth entegrasyonu koddan tamamen kaldırıldı (2026-08-31)

**Karar:** 2026-08-29'da `EmailIntegrations:Enabled=false` flag'iyle kullanıcıdan gizlenen Gmail
OAuth entegrasyonu (Phase 9 — bkz. "Gmail Integration (Phase 9) — kullanıcıdan gizlendi") artık
flag'in arkasında kod olarak da durmuyor, tamamen silindi. Ne bugün ne yakın/orta vadede bu
yatırımın (CASA güvenlik değerlendirmesi, ~$15k-$75k, 4-12+ hafta) yapılması planlanmıyor —
bürokratik ve maddi maliyet kabul edilmedi. Kaldırılanlar:

- Backend: `EmailIntegrationEndpoints.cs`, `IEmailIntegrationService`/`EmailIntegrationService`,
  `IGmailClient`/`GmailClient`/`GmailModels.cs`, `GoogleOAuthOptions`, `EmailIntegrationOptions`,
  `"gmail-sync"` Hangfire job'ı, `Google.Apis.Gmail.v1` paket referansı, `GoogleOAuth`/
  `EmailIntegrations` appsettings/env/docker-compose/CI (`deploy.yml`) girdileri.
- `EmailConnection` entity'sinden Gmail-only alanlar (`EncryptedRefreshToken`, `GrantedScopes`,
  `DisconnectedAt`, `LastSyncedAt`, `LastSyncError`, `LastSyncErrorAt`) ve metodlar (`Reconnect`,
  `Disconnect`, `UpdateAfterSync`, `RecordSyncFailure`) — hepsi doğrulandı: tek çağıranları silinen
  `EmailIntegrationService`'ti. `EmailProvider` enum'ında artık sadece `Forwarding` var.
- `RemoveGmailIntegration` migration'ı bu kolonları drop ediyor ve (uygulama hiç canlıya
  alınmadığı için gerçek kullanıcı riski olmadan) `Provider='Gmail'` satırlarını siliyor.
- Frontend: `emailIntegrations.ts` silindi; Gmail'e özel `settings.email.*` i18n anahtarları
  kaldırıldı; landing page roadmap'indeki "Gmail integration" maddesi gerçekte var olan
  "Email forwarding integration"'ı yansıtacak şekilde güncellendi.
- `PRIVACY_CHECKLIST.md`, `README.md` ("Gmail Integration Setup" bölümü), `DEPLOYMENT.md` da
  buna göre güncellendi.

**Korunanlar — cerrahi ayıklama gerekti:** Gmail ile aynı `EmailConnection`/`EmailSuggestion`
tablolarını ve kısmen aynı servis katmanını paylaşan Forwarding path'ine (Cloudflare Email
Routing, "gerçek yön" — bkz. proje hafızası "Gmail OAuth abandoned, Cloudflare forwarding
chosen") dokunulmadı. `IEmailIntegrationService.GetPendingSuggestionsAsync`/
`ConfirmSuggestionAsync`/`DismissSuggestionAsync` — provider-agnostic oldukları ve
`EmailForwardingEndpoints`'in `/suggestions` route'ları tarafından da çağrıldıkları için —
`IEmailForwardingService`/`EmailForwardingService`'e taşındı (Gmail live-refetch dalı ise atıldı:
Forwarding zaten Subject/Snippet'i her zaman persist ediyor). Bu arada bağımsız bir bug bulundu:
`web/.../settings/email-suggestions/page.tsx` hâlâ `/api/email-integrations/suggestions`'ı
çağırıyordu — ama bu route zaten daha önce `EmailForwardingEndpoints`'e (`/api/email-forwarding/
suggestions`) taşınmıştı, yani sayfa flag'den bağımsız olarak zaten 404 alıyordu; bu kaldırma
işiyle birlikte düzeltildi.

---

## Email forwarding kullanıcıya açıldı: eklenti rehberi + Gmail onay akışı (2026-08-31)

`EmailForwarding:Enabled` `true` yapıldı — özellik artık production'da canlı. Bununla birlikte
eklentiye iki dilli (TR/EN) adım adım bir kurulum rehberi eklendi (`extension/email-forwarding.html`/
`.js`), backend'e Gmail'in kendi yönlendirme-onay mailini tanıyıp kullanıcıya geri gösteren bir akış
eklendi (`EmailConnection.GmailConfirmationCode`/`Link`, `GET /api/email-forwarding/address`,
`POST .../gmail-confirmation/dismiss`), ve store listing (LISTING.md, ekran görüntüleri) güncellendi.

**Gerçek trafikle iki bulgu — ikisi de düzeltildi, uçtan uca doğrulandı:**

1. **Subject eşleşmesi `StartsWith` değil `Contains` olmalıydı.** Gmail'in gerçek onay mailinin
   `From`'u tahmin edildiği gibi tam olarak `forwarding-noreply@google.com`, ama `Subject`'i
   `"(Gmail Forwarding Confirmation - Receive Mail from <adres>"` şeklinde — başında eşleşmeyen bir
   `(` karakteriyle geliyor (bir loglama/encoding artifact'ı değil, `wrangler tail`'de base64 dump
   ile doğrulandı). `subject.TrimStart().StartsWith(...)` bu yüzden hiçbir zaman eşleşmiyordu,
   gerçek onay mailleri sessizce normal sınıflandırmadan geçip düşüyordu.
   `EmailForwardingService.IsGmailForwardingConfirmation`, `subject.Contains(...)`'e çevrildi.

2. **Cloudflare tarafında routing yanlış yapılandırılmıştı — kod değil, altyapı sorunu.**
   `application.ekariyerim.com` zone'unun catch-all kuralı **disabled** (action: drop) durumdaydı —
   yani `test@...` dışındaki hiçbir adrese (gerçek kullanıcı token'ları dahil) gelen mail worker'a
   hiç ulaşmıyordu. Ayrıca var olan tek spesifik kural (`test@application.ekariyerim.com`) gerçek
   `ekariyerim-email-worker`'a değil, bu repoda hiç bulunmayan, önceki bir POC'tan kalma
   `application-inbound-poc` adlı ayrı bir worker'a yönlendiriyordu (kodu incelendi: sadece
   header'ları loglayıp maili doğrudan kullanıcının kendi Gmail'ine forward ediyor — production
   sistemle hiçbir ilgisi yok). Cloudflare dashboard'dan catch-all → `ekariyerim-email-worker`
   olarak düzeltildi, gölgeleyen eski spesifik kural silindi. Bu, README.md'nin "one-time setup"
   olarak belgelediği adımın hiç tam yapılmamış/güncellenmemiş olduğunu gösteriyor — ileride yeni
   bir domain/worker eklenirse Cloudflare dashboard'daki routing rules tablosu koddan bağımsız
   olarak ayrıca doğrulanmalı.

Doğrulama yöntemi: `email-worker/src/index.js`'e geçici bir `console.log` eklenip
(`wrangler deploy`), gerçek bir Gmail hesabından "Add a forwarding address" tetiklenip
`wrangler tail` ile ham `From`/`Subject` yakalandı (ilk denemede base64 encode edilerek, terminal/
JSON formatlamadan kaynaklanabilecek belirsizliği tamamen ortadan kaldırmak için). Düzeltme sonrası
gerçek bir onay maili ile tam uçtan uca doğrulandı: web Ayarlar sayfasındaki "Mail Yönlendirme"
kartında onay kodu/linki doğru göründü, linke tıklanarak Gmail'de forwarding onaylandı.

---

## Production DB'ye yerelden bağlanma: Cloud SQL Auth Proxy artık önerilen yöntem, authorized-networks değil (2026-08-31)

### Önerilen yöntem tersine çevrildi — DECIDED (Sprint 15'teki "authorized-networks, gerçekte kullanılan" kararının yerini alıyor)

Daha önce (bkz. yukarıdaki "Migration bağlantı yöntemi" kararı) authorized-networks yöntemi,
Cloud SQL Auth Proxy'nin bu makinede kurulu olmaması nedeniyle birincil yöntem olarak seçilmişti.
Artık `cloud-sql-proxy` binary'si Homebrew ile kurulu ve `gcloud auth application-default` zaten
yapılandırılmış durumda — bu önceki gerekçeyi geçersiz kılıyor.

**Bulgu:** authorized-networks yöntemi tek seferlik migration için tasarlanmıştı
(`--authorized-networks` ile aç, iş bitince `--clear-authorized-networks` ile kapat). Ama DataGrid
gibi bir GUI istemciyle *tekrarlanan* bağlantılar için bu akış her seferinde IP açıp kapatmayı
gerektiriyor — kullanıcı bunu atladığı için (whitelist migration sonrası temizlenmiş, kullanıcının
ISP IP'si de değişmiş olabilir) DataGrid bağlantısı sessizce kesildi, sebebi ilk bakışta belirsizdi.

**Karar:** DEPLOYMENT.md'nin "Recommended path" olarak işaretlediği yöntem Cloud SQL Auth Proxy'ye
çevrildi (`cloud-sql-proxy --port 5433 <connection-name>`, yerel 5432 çoğunlukla dev Postgres
tarafından kullanıldığı için 5433 kullanılıyor). Proxy arka planda bırakıldığında hem `dotnet ef`
migration'ları hem DataGrid gibi GUI istemcileri aynı `127.0.0.1:5433` üzerinden bağlanabiliyor,
public IP açılmıyor, kapatmayı unutma riski yok. authorized-networks, proxy binary'sinin kurulu
olmadığı bir makinede kullanılacak alternatif olarak dokümanda kaldı.

---

## Pre-LLM email intelligence: `isKnownSender` hard gate → recruitment evidence (2026-09-01)

**Bağlam:** `~/Desktop/e-kariyerim-pre-llm-email-intelligence-plan.md` (kullanıcının kendi notu)
`EmailForwardingService`'in LLM'e gitmeden önceki filtreleme katmanını iyileştirmeyi öneriyordu.
Kullanıcı bunu birebir uygulamak yerine mevcut koda göre gözden geçirilmesini istedi — önleyici bir
iyileştirme olduğu netleştirildi (üretimde gözlemlenmiş somut bir kayıp yok).

**Bulgu — problem çerçevesi kaynak dokümanda göründüğünden daha dar:** `RuleBasedEmailClassifier`
zaten koşulsuz çalışıyor ve curated bir EN/DE/TR ifadesi eşleştiğinde `isKnownSender`'dan bağımsız
sonuç üretiyor (`Inbound_Unknown_Domain_Unmatched_RuleBased_Signal_Creates_Suggestion_Without_Calling_Llm`
testiyle doğrulanmıştı). Asıl boşluk: parafraze edilmiş metinler ve `RuleBasedEmailClassifier`'ın
hiç kuralı olmayan kategoriler (Assessment, Offer, recruiter-sender sezgileri).

**Karar:** Yeni bir `RecruitmentSignalAnalyzer` (Application layer, `RuleBasedEmailClassifier`/
`EmailApplicationMatcher` ile aynı saf/statik desen) eklendi. `EmailForwardingService.ClassifyAsync`,
`RuleBasedEmailClassifier` "NoMatch" döndüğünde artık tek bir `isKnownSender` bool'una değil,
`EmailIntelligence:LlmThreshold` (varsayılan 50) skoruna bakıyor — `isKnownSender` hâlâ hesaplanıyor
ama artık sadece log satırında görünen bir sinyal, hard gate değil.

**Kaynak plandan sapmalar (ve gerekçeleri):**
- Low/Llm/High üç threshold'un v1'de davranışsal farkı yok (hepsi config'te duruyor, sadece
  `LlmThreshold` routing kararını veriyor; Low/High sadece log bucket etiketi).
- `EmailApplicationMatcher.Match`'in `Guid?` dönüşü zenginleştirilmedi (MatchType/Confidence) —
  planın kendi "breaking change yapma" kuralıyla çelişiyordu; `applicationId is not null` zaten
  yeterli bir "matched application" sinyali.
- `JobBoardDomainsOptions` tek bir birleşik ATS/job-board listesi olarak kaldı — ayrı ayrı
  ağırlıklandırmak (`KnownATS` vs `KnownJobBoard`) mevcut portu ve testlerini değiştirmeyi
  gerektirirdi, buna gerek yoktu (`KnownJobBoardOrAts` tek sinyal).
- Analyzer'ın phrase tabloları `RuleBasedEmailClassifier`'ınkiyle **bilinçli olarak paylaşılmadı** —
  analyzer sadece `RuleBasedEmailClassifier` "NoMatch" dönünce çalıştığı için, aynı dar/kesin ifade
  listesini yeniden kullanmak neredeyse hiç sinyal üretmezdi; analyzer'ın listesi kasıtlı olarak
  daha geniş/recall-odaklı.
- `newsletter@`/`marketing@`/`sales@`/`support@`/`billing@` sender local-part'ları negatif değil
  **nötr** ağırlıklandırıldı — body-seviyesi Newsletter/Marketing ifade sinyalleriyle çifte
  cezalandırmayı önlemek için.
- `EmailIntelligenceOptions`/`EmailIntelligenceWeights` Infrastructure değil **Application**
  layer'da yaşıyor (`JobBoardDomainsOptions`'ın aksine) — saf `RecruitmentSignalAnalyzer`'a
  parametre olarak geçtiği için; Infrastructure zaten Application'a bağımlı, tersi olmamalı.
- Golden-dataset JSON fixture klasörü eklenmedi — repodaki hiçbir test JSON fixture yüklemiyor,
  yeni testler mevcut `[Theory]/[InlineData]` konvansiyonunu izliyor.

**Link-domain sinyali için Worker güncellendi (kullanıcı tercihi):** `email-worker/src/index.js`,
`postal-mime`'ın HTML gövdesinden (`parsed.html`) `<a href>` hostname'lerini çıkarıp (asla tam URL —
query string PII taşıyabilir), deduplike edip en fazla 20 tanesini `linkDomains` alanıyla backend'e
gönderiyor. `InboundEmailRequest`/`InboundEmailWebhookRequest` buna göre genişletildi.

**Değişmeyenler:** `RuleBasedEmailClassifier`, `EmailApplicationMatcher`, `JobBoardDomainMatcher`,
`hasSignal` mantığı, matched→extraction bypass, unmatched→extraction, `EmailSuggestion` şeması,
OpenAI provider'ları — hiçbiri değişmedi. Observability v1 için tek bir structured Serilog log
satırı (`score`, `bucket`, `categories`, `isKnownSender`) — yeni metrics altyapısı veya şema
değişikliği yok.

### Ek karar: phrase listeleri de config'e taşındı, eksik config'te uygulama başlamıyor — DECIDED

İlk uygulamada `EmailIntelligencePhrases`'in phrase/domain listeleri (Interview/Assessment/Offer/
Newsletter/... ve `AtsLinkDomains`/`CalendarLinkDomains`) C# tarafında hardcoded default değerlerle
yazılmıştı (sadece `Weights`/threshold'lar appsettings'ten okunuyordu). Kullanıcı bunu **kesinlikle
istemedi**: her sayı/kelime listesi appsettings.json'dan gelmeli ki bir değer değişikliği kod
deploy'u gerektirmesin.

**Karar:**
- `EmailIntelligenceWeights`/`EmailIntelligencePhrases`/`EmailIntelligenceOptions`'taki **her**
  property C# `required` ile işaretlendi, hiçbirinde default değer yok — appsettings.json'un
  `EmailIntelligence` bölümü artık tek kaynak.
- `required`'ın configuration binder tarafından **enforce edilmediği** doğrulandı (küçük bir
  deneyle: eksik bir `required` property sessizce `null`/`0`'a bağlanıyor, exception fırlamıyor) —
  bu yüzden yeni bir `EmailIntelligenceConfigurationValidator` (`IValidateOptions<EmailIntelligenceOptions>`,
  Infrastructure) eklendi. Bağlanmış (bound) objeye değil, **ham `IConfiguration`'a** bakıyor (bir
  int property config'te yoksa 0'a bağlanır, bu da "bilinçli 0" ile "eksik" arasında ayrım
  yapmayı imkânsız kılar — ham config'e bakmak bu belirsizliği ortadan kaldırıyor).
  `EmailIntelligenceOptions`'ın kendi property ağacını reflection ile geziyor, yeni bir
  weight/phrase eklenirse validator'ı güncellemeye gerek kalmıyor.
- `DependencyInjection.cs`'de bu bölüm için `services.Configure<T>(...)` yerine
  `services.AddOptions<T>().Bind(...).ValidateOnStart()` kullanılıyor — `ValidateOnStart()`'ın
  gerçekten `Host.StartAsync()` sırasında `OptionsValidationException` fırlatıp uygulamayı
  başlatmadığı küçük bir deneyle doğrulandı.
- Testler: validator'ın kendisi için 4 unit test (tam config → başarı, eksik weight/boş phrase
  listesi/tamamen boş section → her eksik key'i içeren tek bir hata mesajı). Analyzer'ın kendi
  testleri artık appsettings.json'a değil, test dosyasındaki tam-belirtilmiş literal bir
  `EmailIntelligenceOptions`'a bağlı (mevcut `RuleBasedEmailClassifierTests` konvansiyonuyla
  tutarlı, dosya-yolu kırılganlığından kaçınmak için appsettings.json'u diskten okumak yerine).

## Deploy pipeline: backend ve web artık bağımsız deploy edilebiliyor (2026-09-01)

**Sorun (kullanıcı bulgusu):** `deploy.yml`'in tek `workflow_dispatch: {}` tetikleyicisi hem
`deploy-backend` hem `deploy-web` job'ını birlikte çalıştırıyordu — GitHub Actions
workflow_dispatch'ten tek bir job çalıştırmayı desteklemiyor. Gerçekte değişikliklerin büyük
çoğunluğu sadece bir tarafta oluyor (ya backend ya web), bu yüzden her dispatch'te değişmeyen
tarafın da gereksiz yere image build edip yeniden deploy olması zaman kaybıydı ve ilgisiz bir
image'ın prod'a gitmesi anlamına geliyordu.

**Karar:** Sprint 13'ün "workflow_dispatch-only, bilinçli" kararı (bkz. yukarıdaki "Sprint 13
kararları ve bulguları") artık geçerli değil — o karar GCP kaynaklarının henüz var olmamasına
dayanıyordu, ama gerçek deploy (Sprint 13 — gerçek deploy, 2026-08-26) o günden beri uçtan uca
doğrulanmış durumda. Bu kararı iki değişiklikle değiştiriyoruz:

1. **`push: branches: [main]` artık açık** (DEPLOYMENT.md "9. Switching CI from manual to
   automatic"'in öngördüğü adım) — ama koşulsuz değil: yeni bir `plan` job'ı
   `dorny/paths-filter@v3` ile (repo'da zaten `slack-pr-notify.yml`'in kullandığı desenin aynısı)
   hangi tarafın path'lerinin değiştiğine bakıyor, `contract-check`/`deploy-backend`/`deploy-web`
   sadece ilgili taraf değiştiyse çalışıyor.
2. **`workflow_dispatch` artık bir `target` input'u alıyor** (`both`/`backend`/`web`, varsayılan
   `both`) — kod değişikliği olmadan (ör. secret rotation sonrası) tek bir tarafı veya ikisini
   birden zorla redeploy etmek için.

`deploy-web`'in `deploy-backend`'e `needs` bağımlılığı hâlâ yok (daha önce de yoktu) — `GCP_API_URL`
statik bir secret olduğu için iki job zaten bağımsız, sıralama sadece ilk bootstrap deploy'unda
önemli (DEPLOYMENT.md "4. GitHub repo secrets and first deploy").

**Değişmeyenler:** `contract-check`'in kendisi (Postman koleksiyonu, migration adımı, image
build mantığı) hiçbiri değişmedi — sadece hangi job'ların çalışıp çalışmayacağı artık `plan`
job'unun çıktısına bağlı.

## Deploy sonrası Slack bildirimi: #deployments kanalına commit detayları (2026-09-01)

**Sorun (kullanıcı isteği):** Bir deploy prod'a çıktığında diğer geliştiriciler release'de ne
olduğunu görmüyordu — `slack-pr-notify.yml` sadece PR/merge event'lerini bildiriyor, gerçek
deploy anını değil. Kullanıcı özellikle: deploy tek bir push'tan fazlasını (birden fazla commit)
kapsıyorsa hepsinin detaylarının Slack'te görünmesini istedi.

**Karar (üç açık soru, kullanıcıya soruldu ve karara bağlandı):**
1. **Kanal:** yeni `#deployments` kanalı (mevcut `#pr-backend-merged`'a karıştırılmadı — o PR
   merge event'lerine özel kalıyor).
2. **Mesaj granülerliği:** backend ve web bağımsız deploy olsa da (yukarıdaki karar) tek bir
   birleşik mesaj — `notify-deploy` job'ı `deploy-backend`/`deploy-web`'e `needs` ile bağlı,
   `always()` ile ikisinin sonucunu da bekliyor, hangisi gerçekten deploy olduysa sadece onun
   bölümünü mesaja ekliyor.
3. **Commit aralığı:** son push'un kendi commit listesi değil, **son başarılı deploy'dan bu
   yana** olan tüm commit'ler — `deploy-backend`/`deploy-web` job'ları her başarılı deploy
   sonunda `deploy/api-latest`/`deploy/web-latest` adında bir git tag'i deploy edilen SHA'ya
   force-taşıyor (yüksek-su-işareti deseni); bir sonraki deploy bu tag ile `HEAD` arasındaki
   `git log` farkını alıp Slack mesajına yazıyor. Bu, backend path'i değişmeden geçen ara
   push'ları da (deploy tetiklenmediği için) doğru şekilde bir sonraki backend deploy'una dahil
   ediyor — sadece bu push'un commit'lerine bakmak bunları kaçırırdı.

**Uygulama detayları:**
- `deploy-backend`/`deploy-web` job'larına job-seviyesinde `permissions: {id-token: write,
  contents: write}` eklendi (workflow-seviyesindeki `contents: read`'i override ediyor) — sadece
  tag push'u için, deploy adımlarının kendisi hâlâ sadece `id-token: write` kullanıyor.
  `actions/checkout`'a `fetch-depth: 0` eklendi (tag'e karşı diff almak için tam history gerekli).
- Tag hiç yoksa (ilk deploy) `git log -1 HEAD` ile sadece o anki commit raporlanıyor.
- `notify-deploy`, `needs.*.outputs.count`'a bakıp yeni commit yoksa (ör. secret rotation
  sonrası aynı SHA'nın `workflow_dispatch` ile yeniden deploy'u) Slack'e hiç post atmıyor —
  gürültü olmasın diye.
- Slack payload'ı `actions/github-script` içinde JSON olarak inşa edilip
  `slackapi/slack-github-action`'a doğrudan JSON string olarak veriliyor (YAML içine multiline
  commit listesi gömmenin escaping sorunlarından kaçınmak için) — repodaki diğer workflow'ların
  kullandığı aynı Slack action'ı.
- `SLACK_BOT_TOKEN` secret'ı yeniden kullanıldı (`slack-pr-notify.yml`'deki gibi), yeni bir
  secret eklenmedi. Bot'un `#deployments` kanalına manuel davet edilmesi gerekiyor (workflow
  bunu otomatik yapamaz).

## CI'da integration testleri paralelleştirme denemesi: geri alındı, DOP=1'de kalındı (2026-09-01)

**Sorun (kullanıcı bulgusu):** `ci.yml`'in `backend` job'ı `dotnet test` adımında 5-6 dakika
takılıyormuş gibi görünüyordu (kullanıcı canlı izlerken "iş yapıyormuş gibi bekletiyor" diye
şüphelendi). İncelemede: gerçekten donmuyor, her CI koşumunda tutarlı şekilde 4-6 dakika sürüyor —
"Integration test suite: seri çalıştırma..." (2026-08-29) kararıyla eklenen
`tests/AfterApply.IntegrationTests/xunit.runner.json`'daki `maxParallelThreads: 1` yüzünden ~16
Testcontainers-tabanlı test sınıfı tek tek, seri çalışıyor.

**Kök neden ayrımı:** O `maxParallelThreads: 1` kararı **yerel geliştirme makinesinin paylaşımlı,
resource-constrained rootless-podman VM'i** için alınmıştı (24 container aynı anda başlayınca VM'i
6+ dakika kilitlemişti) — ama aynı `xunit.runner.json` dosyası hem yerel hem CI'da okunuyor, ve CI
(`ubuntu-latest`) paylaşımlı podman VM değil, kendi özel 4 vCPU'lu gerçek Docker daemon'ına sahip
izole bir runner. Yerel VM'i korumak için konan kısıtlama CI'da hiç geçerli olmayan bir sebeple
süreyi 4-16x uzatıyordu — bu teşhis hâlâ doğru, aşağıdaki geri alma sebebi bu değil.

**Denendi:** `xunit.runner.json`'ın kendisine dokunulmadan, `ci.yml`'in `dotnet test` komutuna
VSTest'in RunSettings command-line switch'i eklendi. İlk denemede yanlış casing yüzünden
(`xunit.maxParallelThreads` — küçük harf) hiçbir etkisi olmadığı görüldü (bir teşhis watcher'ı
`docker ps`'i 3sn'de bir loglayarak doğruladı: koşum boyunca hep 1 sınıfın container çifti + Ryuk).
xunit'in resmi RunSettings dokümanı (https://xunit.net/docs/runsettings) doğru formu netleştirdi:
`dotnet test -- xUnit.<Key>=<value>` — PascalCase, çünkü bu switch'ler XML element adına
çevriliyor ve XML büyük/küçük harfe duyarlı. **`README.md`'deki mevcut
`-- xunit.parallelizeTestCollections=false` ipucu da muhtemelen aynı sebeple hiç çalışmıyor —
düzeltilmedi, ayrı bir not.** Doğru casing (`xUnit.MaxParallelThreads=4`) ile watcher gerçekten
aynı anda 9 container'a kadar (4 sınıf × 2 + Ryuk) çıktığını doğruladı, süre 5m50s'den 3m36s'e
düştü.

**Geri alındı — kullanıcı talimatı:** DOP=4'te ve ardından daha ölçülü DOP=2'de, art arda 3
koşumun 3'ünde de (farklı testler: önce `LinkedInImportTests`, sonra iki kez
`EmailForwardingTests`) integration testlerden biri fail oldu — hepsi aynı kök nedene bağlı: bir
Hangfire background job'ı (import işleme, email-suggestion onayı), o sınıfın kendi
`WebApplicationFactory`'siyle aynı anda çalışan başka sınıfların gerçek CPU rekabeti altında,
testin bekleme penceresi içinde bitmiyor — serial (DOP=1) hiç maruz bırakmadığı bir yük profili.
`LinkedInImportTests.PollUntilTerminalAsync`'in kendi yorumu bile bunu önceden öngörmüştü ("under
concurrent test-class load... a trivial import can legitimately take much longer") ama gerçek
paralel çalıştırma ilk kez bu oturumda denendi ve 60s'lik tolerans yetmedi. Kullanıcı "parallel'i 1
yapıp bir şey bozmadığımızdan emin olalım" dedi — `ci.yml` orijinal tek satırlık
`dotnet test AfterApply.slnx --no-build --configuration Release` haline geri döndürüldü, hiçbir
override kalmadı.

**Bilinçli olarak yapılmadı / ileride ele alınabilir:**
- 16 sınıfı tek paylaşımlı `ICollectionFixture`'a indirmek (asıl büyük kazanç, ~16x container
  lifecycle azaltımı) — yukarıdaki "Integration test suite" kararında zaten her sınıfın izolasyon
  varsayımının tek tek incelenmesini gerektirdiği için ertelenmişti; bu oturumda da aynı gerekçeyle
  kapsam dışı.
- Paralelliği tekrar açmak isteyen bir sonraki oturum önce `LinkedInImportTests`/
  `EmailForwardingTests` gibi Hangfire-bekleyen testlerin timeout/polling toleransını gerçek
  paralel yükü karşılayacak şekilde sertleştirmeli (ör. deadline'ı büyütmek veya
  retry/backoff'u genişletmek) — DOP tek başına güvenli değil, bu testler sertleşmeden.

---

## AI Job Matching (Sprint 8) yeniden açıldı — granüler rıza + yurt dışı aktarım disclosure'ı (2026-09-01)

**Bağlam:** Özellik 2026-08-29'da `PRIVACY_CHECKLIST.md`'nin en kritik KVKK açığı gerekçesiyle
kullanıcıdan gizlenmişti (bkz. yukarıdaki "AI Job Matching (Sprint 8) — kullanıcıdan gizlendi"):
CV metni OpenAI'a (ABD) ham hâlde gidiyordu, ama ne granüler bir rıza ne de yurt dışı aktarım
disclosure'ı vardı. Kullanıcı bu iki eksiği kapatıp özelliği production'da açmamı istedi.

**Karar — kapsam bilinçli olarak dar tutuldu:** Bu, tam bir KVKK uyum çalışması değil;
`PRIVACY_CHECKLIST.md`'nin "Avukata götürülecek envanter ve eksikler" listesindeki sadece #2
(granüler rıza) ve #3'ün CV/OpenAI kısmı (yurt dışı aktarım disclosure'ı) kapatıldı. m.10 tam
format, VERBİS muafiyet teyidi, Çerez Politikası, ToS ve Sentry'nin disclosure'ı hâlâ açık —
bunlar için hâlâ bir KVKK avukatına danışılması gerekiyor.

**Uygulama:**
- `CandidateProfile`'a nullable `OpenAiConsentAcceptedAt` eklendi (migration
  `20260901101910_AddOpenAiConsentAcceptedAtToCandidateProfile`) — `Create`/`UpdateCv` her CV
  kaydında bunu `now` ile damgalıyor. Ayrı bir consent parametresi almalarına gerek yok: bu
  metodlar sadece `UpdateCandidateProfileRequestValidator`'ın `OpenAiConsentAccepted == true`
  zaten doğruladığı bir request'ten çağrılabiliyor — yani "buraya ulaşıldıysa rıza verilmiştir".
- `UpdateCandidateProfileRequest`e `OpenAiConsentAccepted: bool` eklendi;
  `RegisterRequestValidator`'daki `ConsentAccepted` kuralıyla birebir aynı desende
  (`Must(x => x)`) zorunlu kılınıyor. Yeni resx key: `VALIDATION_MATCHING_CONSENT_REQUIRED`
  (TR/EN).
- `CandidateProfileResponse`'a rıza timestamp'i **eklenmedi** (YAGNI) — frontend checkbox'ı
  önceki rızaya bakmaksızın her ziyarette işaretsiz başlıyor. Bilinçli tercih: pre-ticked bir
  consent checkbox'ı geçerli açık rıza sayılmaz, bu yüzden her CV kaydında yeniden
  işaretletiliyor (register sayfasındaki genel onaydan farklı olarak, bu onay CV metniyle
  birlikte yenileniyor — `PRIVACY_CHECKLIST.md`'nin #9 "consent versioning" kaygısını bu
  özelliğin kendi kapsamında hafifletiyor).
- Frontend: Ayarlar'daki CV bölümü ve başvuru detayındaki `JobMatchPanel` (2026-08-29'da
  `3bbc775` ile render'dan çıkarılmış, kod silinmemişti) aynen geri eklendi — restore, o
  commit'in diff'inin ters çevrilmesiyle birebir örtüşüyor. Yeni eklenen: register sayfasındaki
  `Checkbox` bileşeninin birebir aynısıyla, `/privacy#cross-border-transfer`'e link veren bir
  onay kutusu; Save butonu CV boşsa veya kutu işaretli değilse disabled; textarea'nın altında
  özel nitelikli veri girmeme uyarısı (checklist #8'in teknik olmayan, hafif bir mitigasyonu).
- `/privacy` sayfasına yeni bir "Yurt dışına veri aktarımı" bölümü (`id="cross-border-transfer"`)
  eklendi: OpenAI, L.L.C. (ABD) isimle anılıyor; amaç, hukuki sebep (spesifik onay kutusu),
  geri çekme yöntemi (CV'yi silmek/hesabı silmek) ve özel nitelikli veri uyarısı ayrı ayrı
  maddelendi. `dataCollection` listesine CV/profil metnini kapsayan bir `item4` eklendi.
- `Matching:Enabled` `appsettings.json`'da `true`'ya çekildi — `EmailForwarding:Enabled`'ın
  2026-08-31'de açıldığı yöntemle birebir aynı mekanizma: `appsettings.Production.json` yok,
  `deploy.yml`/`docker-compose.prod.yml` bu flag'i env var ile override etmiyor, committed
  değer image'a gömülüyor. Yeni migration, mevcut `afterapply-migrate` Cloud Run Job'ı ile
  otomatik uygulanıyor, ekstra bir deploy adımı gerekmedi.

**Doğrulama:** `dotnet test tests/AfterApply.UnitTests` (182/182), `MatchingTests` dahil
podman-backed `dotnet test tests/AfterApply.IntegrationTests --filter Matching` (9/9, yeni
"consent olmadan 400" testi dahil), `npm run build`/`npm run lint` (web). Production'da
`afterapply-openai-api-key` Secret Manager secret'ının gerçek bir anahtar taşıdığı repo'dan
doğrulanamıyor (2 versiyon var, en yenisi 2026-08-31 — muhtemelen gerçek, ama içerik
görülemiyor) — deploy sonrası tek seferlik manuel bir smoke test (Ayarlar'da CV kaydet →
bir başvuruda "Eşleştir" çalıştır) gerekiyor.

---

## AI Job Matching (Sprint 8) ürün kapsamından tamamen kaldırıldı (2026-09-02)

**Bağlam:** Özellik 2026-09-01'de granüler rıza + yurt dışı aktarım disclosure'ı eklenerek
yeniden açılmıştı (bkz. yukarıdaki "AI Job Matching (Sprint 8) yeniden açıldı"). Kullanıcı bu
kez özelliği geçici olarak gizlemek değil, ürün kapsamından kalıcı olarak çıkarmak istedi — CV
metnini OpenAI'a göndererek puanlama yapan akış tamamen kaldırıldı. **OpenAI entegrasyonunun
kendisi kaldırılmadı**: gelen e-postaları sınıflandırıp başvuru durumuna eşleyen ayrı
`EmailIntegrations` özelliği (`OpenAiEmailClassificationProvider`/`OpenAiEmailJobExtractionProvider`)
aynen duruyor ve OpenAI'ı kullanmaya devam ediyor — iki özellik sadece aynı `OpenAiOptions`
(API key/model) config'ini paylaşıyordu, birbirine bağımlı değildi.

**Uygulama:**
- Backend: `AfterApply.Domain/Application/Infrastructure/Matching` klasörlerinin tamamı
  (`CandidateProfile`, `JobMatch`, `IJobMatchingProvider`, `IJobMatchingService`,
  `OpenAiJobMatchingProvider`, `JobMatchingService`, `MatchingOptions`), `MatchingEndpoints`,
  `MatchingRateLimitPolicy`, ilgili EF Core configuration'lar, `AppDbContext`'teki iki `DbSet`,
  `AuthService.DeleteAccountAsync`'teki `CandidateProfiles` temizleme adımı, `Matching`
  appsettings bölümü ve `VALIDATION_MATCHING_CONSENT_REQUIRED` resx anahtarı silindi.
  `Matching` klasöründe yaşayan ama email classifier'ın da kullandığı `OpenAiOptions`,
  `AfterApply.Infrastructure.OpenAi` namespace'ine taşınarak korundu (email özelliği bu sınıfı
  hâlâ `IOptions<OpenAiOptions>` ile inject ediyor).
- DB: geçmiş migration'lara dokunulmadı (`AddJobMatching`,
  `AddOpenAiConsentAcceptedAtToCandidateProfile` olduğu gibi duruyor) — yeni bir
  `RemoveJobMatching` migration'ı `CandidateProfiles`/`JobMatches` tablolarını drop ediyor.
- Frontend: `JobMatchPanel.tsx`, `lib/api/matching.ts`, Ayarlar'daki "CV / Profile" bölümü
  (state, handler, JSX), `types/api.ts`'teki `CandidateProfileResponse`/`JobMatchResponse`/
  `JobMatchRecommendation` silindi. `/privacy` sayfasındaki "Yurt dışına veri aktarımı"
  bölümü (OpenAI'a özel, sadece CV eşleştirme amaçlı disclosure) ve `dataCollection.item4`
  kaldırıldı — kalan disclosure metninde artık CV/OpenAI'dan bahsedilmiyor. Landing page
  roadmap'indeki `todayMatch` ("AI job-fit matching") bullet'ı da kaldırıldı.
- `postman/openapi/openapi.json` ve `postman/collection.json` `dotnet build` + `npm run
  generate` ile yeniden üretildi (elle düzenlenmiyorlar, bkz. `generate-collection.js` başlığı).
- `DEVELOPMENT_PLAN.md`'nin Sprint 8 bölümüne ve `PRIVACY_CHECKLIST.md`'ye (CV/OpenAI'a özgü
  "Yapıldı" satırları N/A'ya geri alındı, envanter tablosundaki CV satırı ve "Eksik" listesinin
  #2/#3/#8 CV kısımları tekrar açık işaretlendi) kaldırıldığını belirten notlar eklendi —
  geçmiş kararlar silinmedi, sadece güncel durum eklendi.

**Kapsam dışı / bilinçli olarak dokunulmadı:** `EmailIntegrations` modülü, paylaşılan
`OpenAiOptions`/API key, `postman/collection.json`'daki email-classifier ile ilgili kayıtlar.

**Doğrulama:** `dotnet build` (Api + Infrastructure + iki test projesi) hatasız; `npx tsc
--noEmit`, `npx eslint`, `npm run build` (web) hatasız.

---

## AI Job Matching kaldırmasının artçıları: extension taraması + email/OpenAI disclosure'ı (2026-09-02)

**Bağlam:** Yukarıdaki kaldırma sonrası kullanıcı iki şey istedi: (1) browser extension'da özelliğin
bir kalıntısı kalmadığından emin olmak, (2) bir önceki turda `PRIVACY_CHECKLIST.md` güncellenirken
fark edilen bir eksiği ("email sınıflandırmasının OpenAI'a gönderdiği subject/snippet hiç
disclosure edilmemiş") şimdi ele almak.

**Extension taraması:** Kod tabanında gerçek bir kalıntı yoktu — sadece iki yorum "AI Job Matching"e
referans veriyordu: `extension/popup.js` (job description'ın plain-text tutulma gerekçesi) ve
`src/AfterApply.Domain/Jobs/Job.cs`/`Responses.cs` (aynı gerekçe, backend tarafında). Bu ikisi
düzeltildi. Daha önemlisi: bu tarama sırasında `ApplicationDetailResponse.JobDescription` (plain
text) alanının artık **hiçbir tüketicisi kalmadığı** ortaya çıktı — tek kullanım yeri, kaldırılan
`JobMatchPanel`'in textarea'sını pre-fill etmekti (`initialJobDescription` prop'u). Bu alan
backend (`ApplicationService.ToDetailAsync`, `Responses.cs`) ve frontend (`types/api.ts`) tarafında
temizlendi; `Job.Description` domain alanının kendisine dokunulmadı (extension capture'ı ve email
job-extraction provider'ı hâlâ dolduruyor, `DescriptionHtml`'in sibling'i olarak genel amaçlı bir
alan) — sadece artık kullanılmayan API-response projeksiyonu kaldırıldı.

**Email/OpenAI disclosure'ı:** `/privacy` sayfasına, kaldırılan CV/OpenAI bölümüyle aynı yapıda
ama `EmailIntegrations`'a özgü içerikle yeni bir "Yurt dışına veri aktarımı" bölümü eklendi (aynı
`id="cross-border-transfer"` anchor'ı yeniden kullanıldı) — OpenAI, L.L.C. (ABD) isimle anılıyor;
kapsam sadece forward edilen statü e-postalarının Subject + kısa bir Snippet'i (tam e-posta gövdesi
asla), ve sadece yerel kural tabanlı sınıflandırıcı ("RuleBasedEmailClassifier") bir eşleşme
bulamadığında tetiklendiği açıkça belirtildi. `dataCollection`'a bunu kapsayan bir `item4` eklendi.
**Bilinçli olarak eklenmedi:** CV/OpenAI'daki gibi ayrı bir granüler onay kutusu — bu, Mail
Forwarding kurulum akışına yeni bir consent adımı eklemeyi gerektiren ayrı bir ürün kararı, sadece
disclosure istenmişti (bkz. `PRIVACY_CHECKLIST.md` Eksik #2, hâlâ açık).

**Ayrıca düzeltildi — checklist'teki gerçek bir hata:** `PRIVACY_CHECKLIST.md`'nin envanter
tablosu, `EmailSuggestion.Subject`/`Snippet`'in "DB'ye yazılmadığını, sadece bellekte kullanılıp
atıldığını" iddia ediyordu. Kod böyle çalışmıyor — `EmailSuggestionConfiguration`
(`HasMaxLength(500)`/`HasMaxLength(2000)`) bu iki alanı açıkça map'liyor ve `EmailForwardingService`
her iki `EmailSuggestion.Create`/`CreateForNewJob` çağrısına `request.Subject`/`request.Snippet`'i
geçiriyor — kullanıcının bir öneriyi incelerken görebilmesi için bilinçli bir tasarım. Sadece
e-postanın **tam gövdesi (body)** hiç persist edilmiyor; checklist'in "veri minimizasyonu" iddiası
o kısım için doğruydu, ama subject/snippet için yanlıştı. Envanter tablosu ve "Olumlu noktalar"
bölümü buna göre düzeltildi.

**Doğrulama:** `dotnet build` (Api + Infrastructure) hatasız; `npx tsc --noEmit`, `npx eslint`,
`npm run build` (web) hatasız.

---

## E-posta öneri akışı gerçek tarayıcıda uçtan uca doğrulandı (2026-09-02)

**Bağlam:** Kullanıcı iki şeyden emin olmak istedi: (1) daha önce hiç başvurusu olmayan bir şirket
dönüş yaptığında adaya gerçekten bir öneri sunuluyor mu, (2) mevcut bir başvuruya statü güncellemesi
(red/mülakat/vb.) geldiğinde bu, e-kariyerim'deki başvurunun durumuna gerçekten yansıyor mu. Kod
okuması yeterli görülmedi — Cloudflare Worker'ın kullandığı gerçek `/api/email-forwarding/inbound`
webhook'u yerel ortamda simüle bir e-postayla tetiklenip Chrome'da uçtan uca test edildi.

**Doğrulanan akış:** simüle inbound e-posta → Hangfire arka plan işi → `RuleBasedEmailClassifier`/
`RecruitmentSignalAnalyzer` sınıflandırması → (eşleşmeyen gönderen için) gerçek OpenAI çağrısıyla
şirket/pozisyon çıkarımı → `EmailSuggestion` kaydı → `/suggestions` sayfasında görünüm → "Onayla"
tıklaması → (yeni şirket için) gerçek `Application`/`Company` oluşumu, (mevcut başvuru için) gerçek
`ApplicationStatusHistory`/`ApplicationEvent` kaydıyla durum değişikliği. Her iki senaryo da
başvuru listesinde ve zaman çizelgesinde beklenen sonucu verdi — placebo değil, gerçek DB yazımı.

**Tespit edilen bir tuzak (kod hatası değil, test yazarken dikkat edilmesi gereken bir nokta):**
`RuleBasedEmailClassifier`'daki kalıplar Türkçe karakterlere birebir duyarlı (ör. "mülakata davet")
— ASCII'ye indirgenmiş bir metin ("mulakata davet") kuralı sessizce kaçırıyor, `RecruitmentSignalAnalyzer`
skoru 0 çıkabiliyor ve hiçbir hata vermeden öneri hiç oluşmuyor.

**Kalıcı test altyapısı bilinçli olarak yerinde bırakıldı:** yerel API+web+Postgres+Redis yığını ve
adanmış bir test kullanıcısı/başvuruları, bu akışı hızlıca yeniden test edebilmek için ayakta ve
dokunulmadan tutuluyor — kimlik bilgileri burada değil, proje hafızasında tutuluyor.

**Bundan sonraki geliştirmeler için not:** `EmailForwardingService`, `RuleBasedEmailClassifier`,
`RecruitmentSignalAnalyzer`, `EmailApplicationMatcher`, `EmailSuggestion` entity'si veya
`/api/email-forwarding/*` / `/suggestions` sayfasına dokunan her değişiklikten sonra, sadece unit
testlere güvenmek yerine bu iki akış (yeni şirket → öneri → onay → yeni başvuru; eşleşen başvuru →
öneri → onay → durum güncellemesi) yukarıdaki test altyapısıyla tarayıcıda yeniden doğrulanmalı —
regresyonları erken yakalamak için.

---

## Email-forwarding webhook güvenilirliği: Worker'da bounded retry eklendi, Queue/alarm ertelendi (2026-09-02)

**Bağlam:** Cloudflare Email Routing dashboard'da son 7 günde 18 "Delivery failed" görüldü.
İnceleme sonucu bunların `email-worker`'ın hiç var olmadığı tarihlere (28-29 Ağustos; worker'ın ilk
commit'i `9a400ef`, 31 Ağustos) ait olduğu, dolayısıyla mevcut koddan kaynaklanmadığı anlaşıldı —
muhtemelen erken routing rule/destination address kurulum denemeleri. Daha önemlisi: mevcut worker
kodu (`email-worker/src/index.js`) webhook `fetch` hatalarını (network hatası veya non-2xx yanıt)
hiçbir zaman throw etmiyor, sadece `console.error` ile yutuyor — yani Cloudflare'ın "Delivery failed"
metriği backend API'deki gerçek kesintileri hiç yakalamıyor ve alarm kaynağı olarak kullanılamaz.

**Karar:** `email-worker/src/index.js`'e webhook çağrısı için bounded retry eklendi (3 deneme,
300ms tabanlı artan gecikme — `WEBHOOK_MAX_ATTEMPTS`/`WEBHOOK_RETRY_BASE_DELAY_MS`). Hâlâ throw
edilmiyor: `email()` handler'ından exception fırlatmak, Cloudflare'ın mesajı orijinal gönderene
(bir recruiter/ATS) bounce olarak geri döndürmesine yol açabilir — bu, sessizce mesajı kaybetmekten
daha kötü bir dış-görünür sonuç olurdu.

**Ertelenen alternatifler (sorun büyürse tekrar değerlendirilecek):**
- **Cloudflare Queue + dead-letter queue:** retry'lar tükendiğinde payload'u loglamak yerine bir
  Queue'ya yazıp ayrı bir consumer'la Cloudflare-yönetimli backoff/DLQ ile tekrar denemek — gerçek
  durable recovery sağlar (email kaybolmaz), ama yeni binding/consumer + Workers Paid plan kontrolü
  gerektiriyor.
- **Ayrı alarm (ör. Slack webhook):** retry'lar tükendiğinde worker'dan tek satır bir bildirim POST'u
  atmak. Cloudflare'ın kendi native delivery-failure bildirimi **yok** — community'de uzun süredir
  istenen ama hâlâ shippenmemiş bir özellik olduğu teyit edildi, o yüzden dashboard'a dayanan bir
  alarm mümkün değil, kendi alarm mekanizmamız gerekir.
- Kullanıcı bilinçli olarak şimdilik bu ikisini ertelemeyi tercih etti; inline retry'ın yeterli
  olduğu değerlendirildi. Uzun vadede tekrar sorun yaşanırsa bu not başlangıç noktası olsun.

**Doğrulama:** `node --check email-worker/src/index.js` hatasız. `email-worker`'da otomatik test
altyapısı yok (repo'da hiç test dosyası yok, `package.json`'da test script'i tanımlı değil).

---

## GitHub Dependabot açıkları giderildi: wrangler 4'e geçiş + postman override'ları, faker kalıntı riski kabul edildi (2026-09-02)

**Bağlam:** GitHub'a push sonrası 14 açık Dependabot uyarısı görüldü (3 high, 9 moderate, 2 low).
Hepsi iki bağımsız dev-tooling zincirinden geliyordu, runtime/uygulama koduna hiç girmiyordu:
`email-worker/package-lock.json` (11 uyarı — `wrangler@3.x`'in transitive bağımlılıkları: `sharp`,
`ws`, `undici`, `esbuild`) ve `postman/package-lock.json` (3 uyarı — `openapi-to-postmanv2@6.3.3`
üzerinden gelen `js-yaml`, `uuid`, `yaml`).

**Karar — email-worker:** `wrangler` `^3.90.0` → `^4.128.0`'a yükseltildi. `wrangler deploy --dry-run`
ile config uyumluluğu doğrulandıktan sonra gerçek deploy yapıldı (Version ID `50fa8541-...`).
Sonuç: 0 açık kaldı.

**Karar — postman:** `openapi-to-postmanv2` zaten npm'deki en güncel sürümde (`6.3.3`) sabitliydi,
daha yeni bir sürüm yok — üst paketi yükseltmek mümkün değildi. **Bir ara adımda yanlışlıkla
`4.18.0`'a düşürüldü** (Dependabot alert'inin "patched" alanını üst pakete ait sanıp kopyalamıştım),
bu da açık sayısını 3'ten 8'e çıkardı; hemen fark edilip `git checkout` ile committed lockfile'a geri
dönüldü. Doğru çözüm: `package.json`'a nested transitive bağımlılıkları zorlayan bir `overrides`
bloğu eklendi (`js-yaml: 4.3.1`, `uuid: 11.1.1`, `yaml: 1.10.3`) — üst paket sürümü değişmeden. Bu
üçü test edildi: `npm run generate` sorunsuz çalıştı, üretilen `collection.json` git'teki mevcut
haliyle birebir aynı çıktı (deterministic, diff yok).

**Kabul edilen kalıntı risk — `@faker-js/faker` (high, `postman-collection`'ın transitive bağımlılığı):**
`postman-collection@5.3.1`, `@faker-js/faker`'ı tam `5.5.3`'e sabitlemiş ve kendi kodu
(`superstring/dynamic-variables.js`) o sürümün eski API'sini (`faker.address.city` vb.) doğrudan
çağırıyor. Advisory'nin patched sürümü (`10.5.0`+) bu API'yi hiç içermiyor — `address` namespace'i
faker v8'de `location`'a yeniden adlandırılmış. `overrides` ile faker'ı zorlamak denendi,
`npm run generate` şu hatayla anında çöktü: `TypeError: Cannot read properties of undefined
(reading 'city')`. Yani upstream `postman-collection` bu CVE'yi (arbitrary code execution via
`helpers.fake`) breaking bir major sürüm atlamadan düzeltemiyor durumda. **Kabul gerekçesi:** bu
araç sadece CI'da, kendi güvendiğimiz OpenAPI dokümanımızı işliyor — dışarıdan/kullanıcıdan gelen
girdiyi hiç işlemiyor, pratik sömürülebilirlik yok. Üst akış (`postman-collection`/`openapi-to-postmanv2`)
faker'ı günceller veya `patch-package` gibi bir hack'e gerek duyulursa bu not başlangıç noktası olsun.

**Sonuç:** 14 açıktan (email-worker 11 + postman 3) email-worker'daki tamamı ve postman'daki
js-yaml/uuid/yaml giderildi; sadece postman'daki faker (1 high) yukarıdaki gerekçeyle bilinçli
olarak açık bırakıldı.

**Doğrulama:** her iki dizinde `npm audit` (email-worker: 0 açık; postman: sadece faker, 3 alt-advisory
tek pakette). `postman`'da `npm run generate` başarıyla çalıştı, çıktı git'teki mevcutla birebir aynı.

> **Düzeltme (2026-09-03):** Yukarıdaki "çıktı git'teki mevcutla birebir aynı" ifadesi yanlış.
> Ölçüldü: `openapi-to-postmanv2` her koşuda yeni UUID'ler ve rastgele örnek tarihler üretiyor, yani
> `collection.json` **hiçbir zaman** deterministik değildi — gerçek faker ile arka arkaya iki üretim
> de birbirinden farklı çıkıyor. Bu yüzden koleksiyonu bayt bayt karşılaştırmak anlamsız; doğru
> karşılaştırma `id`/`_postman_id` alanlarını ve üretilen tarihleri hariç tutan yapısal bir
> karşılaştırma. Kabul edilen faker riski de aşağıdaki kayıtla kapatıldı.

---

## Email forwarding (forward-all-inbox-to-us) tamamen kaldırıldı, Gmail Taraması tek email-signal akışı oldu (2026-09-03)

**Karar:** Kullanıcı, güncellenmiş eklentide "Mail Yönlendirmeyi Kur" butonunun hâlâ eski "tüm gelen
kutunu bize yönlendir" akışını açtığını fark etti — Gmail Taraması (extension-signal, e1f765c)
zaten bu akışı **birincil** yol olarak değiştirmek üzere eklenmişti ama forwarding path o commit'te
kasıtlı olarak dokunulmadan bırakılmıştı (bkz. e1f765c commit mesajı). Bu sefer kullanıcı forwarding'in
artık hiç var olmaması gerektiğine karar verdi — kısmi/geriye dönük uyumluluk hack'i değil, tam
kaldırma. Provider-agnostic paylaşılan altyapı (`/suggestions*`, `/notifications*`,
`EmailSuggestion`, `ProcessSignalAsync` pipeline'ı) dokunulmadan kaldı; yalnızca forwarding'e özel
her şey söküldü.

**Kaldırılanlar:**

- **Backend:** `EmailForwardingEndpoints.cs`'ten `GET /address`, `POST /gmail-confirmation/dismiss`,
  `POST /inbound` route'ları; `EmailForwardingService`'ten `GetOrCreateInboundAddressAsync`,
  `ProcessInboundEmailAsync`, `DismissGmailConfirmationAsync`, Gmail-onay-maili tespiti (regex'ler
  dahil); `EmailConnection`'dan `InboundToken`/`GmailConfirmationCode`/`GmailConfirmationLink`/
  `GmailConfirmationReceivedAt` alanları ve `CreateForwarding`/`SetGmailConfirmation`/
  `ClearGmailConfirmation` metodları; `EmailProvider.Forwarding` enum üyesi (artık tek üye:
  `Extension`); `InboundAddressResponse`/`InboundEmailRequest`/`InboundEmailWebhookRequest`
  contract'ları; `EmailForwardingOptions.Domain`/`WebhookSecret` (yalnızca `Enabled` kaldı — grup
  genelinde kill switch olarak, `/extension-signal` dahil); `InboundEmailRateLimitPolicy`.
  `EmailConnections` tablosundan bu kolonları drop eden ve mevcut `Provider='Forwarding'`
  satırlarını (cascade ile `EmailSuggestions`'ları da) silen yeni bir migration eklendi —
  `RemoveGmailIntegration` migration'ıyla aynı desen (bkz. yukarıdaki "Gmail OAuth entegrasyonu"
  kaydı).
- **Cloudflare Email Worker (`email-worker/`) dizini tamamen silindi** — tek amacı `/inbound`'a
  relay etmekti, başka hiçbir işlevi yoktu. `DEPLOYMENT.md`'deki secret-oluşturma adımı
  (`afterapply-email-forwarding-webhook-secret`) ve `deploy.yml`'deki
  `EmailForwarding__WebhookSecret` env var wiring'i kaldırıldı — GCP'deki secret'ın kendisi bu
  değişiklikle silinmedi, yalnızca artık hiçbir yerden referans edilmiyor.
- **Extension:** `email-forwarding.html`/`.js` (adım adım Gmail kurulum rehberi) silindi;
  `options.html`/`.js`'ten "Mail Yönlendirme" bölümü/butonu kaldırıldı; `i18n.js`'ten `hero`/`flow`/
  `address`/`steps`/`faq` blokları (TR+EN) ve `options.forwardingLabel`/`forwardingHelp`/
  `setUpForwarding` anahtarları silindi. Store listing (`LISTING.md`, `PRIVACY_POLICY.md`,
  `PERMISSIONS_JUSTIFICATION.md`, ekran görüntüsü README'si) Gmail Taraması'nı tek email-signal
  özelliği olarak anlatacak şekilde yeniden yazıldı; `forwarding-light/dark.png` ve
  `scene-forwarding.html` silindi (henüz Chrome Web Store'a gönderilmemiş taslak, canlı listing
  etkilenmedi).
- **Web app:** Settings sayfasındaki "Mail Forwarding" kartı (adres/onay-kodu UI'ı) ve ona bağlı
  state/handler'lar kaldırıldı; `emailForwardingApi.getAddress`/`dismissGmailConfirmation` ve
  `InboundAddressResponse` tipi silindi (provider-agnostic `getPendingSuggestions*`/
  `confirmSuggestion`/`dismissSuggestion` korundu — hâlâ kullanılıyor). Help sayfalarındaki
  ("Settings", "Chrome Extension") forwarding'e özel bölümler ve ilgili ekran görüntüleri
  kaldırıldı; onboarding/FAQ/gizlilik metinleri (`en.json`/`tr.json`) forwarding yerine Gmail
  Taraması'nı anlatacak şekilde güncellendi.
- **Testler:** `EmailForwardingTests.cs` (forwarding-mekanik testleri: adres oluşturma, webhook
  secret doğrulama, bilinmeyen token, Gmail onay akışı — hepsi silindi) `EmailSignalTests.cs`
  olarak yeniden yazıldı; paylaşılan pipeline'ı (eşleştirme/sınıflandırma/auto-apply/confirm/
  dismiss/notifications) sınayan testler `/inbound` yerine `/extension-signal` üzerinden
  çalışacak şekilde dönüştürüldü, kapsam kaybı olmadan.

**Korunanlar:** `/api/email-forwarding` route namespace'i aynı kaldı (zaten kurulu eklenti
versiyonlarıyla uyumluluk için — artık "forwarding" anlamına gelmiyor, sadece tarihsel isim);
`EmailForwardingOptions`/`EmailForwardingService`/`EmailForwardingEndpoints` sınıf adları da aynı
sebeple değiştirilmedi (internal/infra identifier, CLAUDE.md'nin AfterApply/e-kariyerim ayrımıyla
aynı mantık).

**Doğrulama:** `dotnet build src/AfterApply.Api` hatasız; `postman/collection.json` yeniden
üretildi (47 request), kaldırılan üç route (`/address`, `/gmail-confirmation/dismiss`, `/inbound`)
çıktıda yok. Extension/web JSON i18n dosyaları `python3 -c "json.load(...)"` ile doğrulandı.
Podman/integration test koşusu bu batch'in sonuna bırakıldı (bkz. proje hafızası "Podman test
cadence").

---

## OWASP güvenlik incelemesi ve düzeltme planı (2026-09-03)

**Bağlam:** Uygulamanın tamamı (API, web, extension, deploy/CI zinciri) OWASP Top 10 (2021),
OWASP API Security Top 10 (2023), OWASP LLM Top 10 ve ASVS L2 referans alınarak tarandı. Sonuç:
3 yüksek, 6 orta, 8 düşük/sertleştirme bulgusu. Klasik ölümcül sınıflar zaten kapalıydı —
SQL injection yüzeyi yok (hiç raw SQL yok), IDOR sistematik olarak `userId` scope'uyla kapatılmış
(SignalR hub'ında grup katılımı dahil), SSRF savunması allow-list + `AllowAutoRedirect=false` +
her redirect hop'unda yeniden doğrulama ile örnek düzeyde, import pipeline'ında zip bomb/zip slip
korumaları yerinde, refresh token'lar hash'li + rotasyonlu + reuse detection'lı, secret yönetimi
WIF + Secret Manager üzerinden. Bulguların ağırlığı "şu an sömürülüyor" değil, "bir XSS/bir bot
çıktığında hiçbir katman durdurmaz" kategorisinde.

**Uygulananlar:**

1. **Faz 1 — üretimde fiilen bozuk olan.** `UseForwardedHeaders` (Cloud Run TLS'i frontend'de
   sonlandırıp container'a düz HTTP ile geçiyor; uygulama gerçek istemci IP'sini değil proxy'yi
   görüyordu → IP'ye göre bölünen auth rate-limit'i **tüm dünya için tek partition**'a düşüyordu,
   yani dakikada toplam 5 login denemesi; ayrıca `UseHttpsRedirection` sessizce no-op'tu ve
   `RefreshToken.CreatedByIp` proxy'yi kaydediyordu). `ForwardLimit` varsayılan 1'de bırakıldı —
   middleware `X-Forwarded-For`'u sağdan okur ve Cloud Run gerçek IP'yi **sona ekler**, dolayısıyla
   istemcinin gönderdiği sahte değer asla seçilmez. `/extension-signal` artık
   `ExtensionEmailSignalRequestValidator`'dan geçiyor (Subject/Snippet cap'leri kasten
   `EmailSuggestionConfiguration`'daki kolon uzunluklarını yansıtıyor: fazla uzun metin eskiden
   uçtan geçip Hangfire job'ının içinde `SaveChangesAsync`'te patlıyor, asla başarılı olamayacak bir
   isteği 10 kez retry ediyordu). Web'e CSP/HSTS/X-Frame-Options/Referrer-Policy/Permissions-Policy,
   API'ye `default-src 'none'` CSP + `nosniff` + HSTS.
2. **Faz 2 — kimlik doğrulama sertleştirme.** Parola politikası Identity varsayılanı 6'dan 12'ye
   (istemci zaten 8 istiyordu — sunucu ikisinin **zayıf** olanıydı); lockout ayarları da açıkça
   yazıldı. PAT'e `Scope` + `ExpiresAt` (90 gün) eklendi; yeni token'lar varsayılan olarak
   `Extension` kapsamlı ve yalnızca eklentinin gerçekten çağırdığı üç uca erişiyor
   (`AllowExtensionToken()` ile işaretli). Zorlama **default authorization policy**'ye takıldı, tek
   tek uçlara değil — böylece sonradan eklenen bir uç, biri açıkça izin vermedikçe extension
   token'ının erişemeyeceği yerde kalır. `PersonalAccessTokenService`'in doğrulama cache TTL'i
   60→15 sn (HybridCache'in L1'inde backplane yok, iptal edilen token diğer instance'larda TTL
   kadar yaşıyor — gerçek iptal gecikmesi bu).
3. **Faz 3 — kaynak tüketimi + sertleştirme.** `GlobalLimiter` (kullanıcı/IP başına 300 istek/dk;
   öncesinde yalnızca 3 named policy vardı, `/api/users/me/export` ve `/api/companies/search` gibi
   uçlar tamamen sınırsızdı); `/resolve-link` için ayrı ve daha sıkı `link-preview` policy'si (tek
   dışa HTTP isteği atan uç — global limitte kalsaydı bir hesap dakikada yüzlerce isteği bizim
   IP'mizden başkasının sunucusuna yöneltebilirdi); 429'lara `Retry-After`. Extension'ın
   `escapeHtml`'i artık `"` ve `'` de kaçırıyor (attribute breakout; Company satırları **global**
   olduğu için başka bir kullanıcının oluşturduğu ad herkesin autocomplete'ine düşüyor — MV3 CSP'si
   inline script'i bloklamasa doğrudan XSS olurdu). `JobUrl` alanlarına `MustBeAWebUrl()`
   (`javascript:` şeması saklanıp `<a href>` olarak render ediliyordu — React tehlikeli şemaları
   filtrelemez). PAT scheme selector `Contains` → `StartsWith("Bearer aa_pat_")`. Her iki container
   non-root.
4. **Faz 4 — süreç.** CI'ya `dependency-audit` job'ı (`dotnet list package --vulnerable` +
   `npm audit --audit-level=high`) ve CodeQL workflow'u (C# + JS/TS, `security-extended`; repo public
   olduğu için ücretsiz). Bu gate'in ilk bulgusu hemen çıktı: Hangfire.Core'un
   `Newtonsoft.Json >= 11.0.1` tabanı, test projelerinde literal 11.0.1'e çözülüyordu
   (GHSA-5crp-9r3c-p9vr, high) — API/Infrastructure'da tesadüfen `EntityFrameworkCore.Design` tabanı
   13.x'e çekiyordu ama Design bir development dependency, asset'leri test projelerine akmıyor.
   `CentralPackageTransitivePinningEnabled` + merkezi `Newtonsoft.Json 13.0.4` pin'i ile kapatıldı.

**Uygulanmayan bir madde ve nedeni — `AllowedHosts`:** İlk planda `AllowedHosts: "*"` daraltılacaktı
(L6). İncelendiğinde faydasının bu mimaride sıfır olduğu görüldü: Host header'ının klasik istismarı
şifre sıfırlama linkini zehirlemektir, ama `AuthService.ForgotPasswordAsync` linki request host'undan
değil `AppOptions.WebBaseUrl` (config) üzerinden kuruyor; Host'a göre anahtarlanan paylaşımlı bir
cache de yok. Buna karşılık Cloud Run'ın startup/liveness probe'ları container'a kendi iç
adresleriyle bağlanıyor ve dar bir allow-list bu probe'ları 400'e düşürüp revision'ı hiç ayağa
kaldırmayabilir. Gerçek fayda yokken gerçek deploy riski alınmadı.

**Doğrulama:** `dotnet build AfterApply.slnx` uyarısız; 182 unit test geçti; `PersonalAccessTokenTests`
(yeni kapsam/expiry testleri dahil, 11 test) Testcontainers ile geçti; `npx tsc --noEmit` ve
`npm run lint` temiz; `next build` başarılı. Kapsam sınırı iki mevcut PAT testinin davranışını
kasten değiştirdiği için o testler `Full` kapsam isteyecek şekilde güncellendi ve sınırın kendisi
için ayrı testler eklendi (izinli uçta 200, diğer her yerde 403 — 401 değil: kimlik geçerli,
yetki yok).

## E-posta doğrulaması bilinçli olarak ertelendi — Resend free plan kotası (2026-09-03)

**Karar:** Yukarıdaki güvenlik incelemesinin **M1** bulgusu (kayıt sırasında e-posta doğrulaması
yok) bu turda **kasıtlı olarak düzeltilmiyor**. Gelecekte yapılacaklar listesine alındı.

**Gerekçe:** Giden e-posta Resend üzerinden gidiyor ve hesap **free plan'de: günlük 100 e-posta
hakkı** var. Bu kota şu anda tamamen şifre sıfırlama (`SendPasswordResetEmailAsync`) ve şifre
değişti bildirimi (`SendPasswordChangedEmailAsync`) için ayrılmış durumda. Kayıt akışına zorunlu
doğrulama maili eklemek, her yeni kullanıcı için en az bir mail (pratikte "tekrar gönder"lerle
daha fazlası) demek — mevcut kotayı hızla tüketip **şifre sıfırlama akışını çalışmaz hale
getirme** riski taşıyor. Doğrulanmamış hesabın riski (hesap squatting, doğrulanmamış adrese mail
gitmesi) ile şifre sıfırlamanın kota dolduğu için sessizce başarısız olması karşılaştırıldığında,
ikincisi bugün daha ağır basıyor.

**Şu anki risk kabulü:** Herkes başkasının e-posta adresiyle hesap açabilir; `RequireUniqueEmail
= true` olduğu için gerçek adres sahibi o e-postayla kayıt olamaz (squatting). Kullanıcı tabanı
küçükken kabul edilebilir; büyüdükçe kabul edilemez hale gelir.

**Gelecekte yapılacak — tetikleyici koşullar (herhangi biri):** Resend'de ücretli plana geçildiğinde,
ya da günlük e-posta hacmi kotanın %50'sine yaklaştığında, ya da ilk squatting/kötüye kullanım
vakası görüldüğünde. Uygulama tarafı hazır: `AddDefaultTokenProviders()` zaten kayıtlı,
`IEmailSender`/`EmailTemplates` altyapısı ve `DataProtectionTokenProviderOptions.TokenLifespan`
(30 dk) mevcut — eklenmesi gereken `RequireConfirmedAccount`, bir `EmailConfirmation` template
satırı ve confirm endpoint'i. Ara çözüm olarak, tam doğrulamadan önce kayıt ucuna disposable-domain
reddi gibi mailsiz bir önlem konabilir.

---

## Integration test altyapısı: container'lar assembly başına paylaşılıyor (2026-09-03)

**Bağlam:** Kullanıcı testlerin hem yerelde hem CI'da çok uzun sürmesinden ve "pipeline'ın takılıp
kalmasından" şikayet etti. Ölçüm iki ayrı sorun olduğunu gösterdi ve bunları ayırmak bu kaydın asıl
amacı.

**Bulgu 1 — maliyet sanılandan çok daha büyüktü.** Her test sınıfı container'larını *instance field*
olarak tanımlıyordu:

```csharp
private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
```

xunit her test **metodu** için sınıfın yeni bir örneğini kurar — yani bu field her testte yeniden
çalışıyordu. İsraf "17 sınıf × 2 container" değil, **~107 test × 2 container** ve **~107 kez tam
migration zinciri**ydi. İlk teşhiste bunu 17 fixture diye raporlamak yanlıştı; gerçek sayı ancak
Postgres'te oluşan veritabanları sayılınca ortaya çıktı.

**Karar:** `SharedInfrastructure` (xunit `ICollectionFixture`) assembly başına **tek** Postgres +
**tek** Redis kaldırıyor, şema **bir kez** bir template veritabanına migrate ediliyor, her test
`CREATE DATABASE ... TEMPLATE` ile onu klonluyor. İzolasyon aynı kalıyor: her test hâlâ kendine ait,
boş ama migrate edilmiş bir veritabanı ve kendine ait bir Redis veritabanı alıyor. Paralellik
**değişmedi** — tüm sınıflar tek collection'da, `maxParallelThreads=1` yerinde; 2026-09-01'de
denenip geri alınan DOP>1 konusuna hiç girilmedi.

**Sonuç:** 123 container → 2. Koşu tamamlandığında **107/107 test, 91 saniye** (test başına ~2.6s →
~0.85s). Refactor öncesi tam suite bu makinede zaten baştan sona koşamıyordu.

**Yol boyunca çıkan ve kayda değer tuzaklar** (hepsi ölçülerek bulundu, tahminle değil):

- `postgres` image'ının entrypoint'i komut tire ile başlıyorsa binary'yi kendisi ekler.
  `WithCommand("postgres", "-c", ...)` container'ı `invalid argument: "postgres"` ile düşürür;
  yalnızca flag'ler verilmeli.
- `NpgsqlConnection.ClearAllPools()` **kullanılmamalı**. Her test farklı veritabanına bağlandığı için
  her test kendi havuzunu bırakır ve bu birikir; ClearAllPools bunu temizler ama süreç genelinde
  çalışır ve önceki testin Hangfire sunucusu hâlâ kapanıyor olabilir. Arka plan thread'lerinin
  altından bağlantı çekmek test host'unu komple çökertti. Doğru kaldıraç
  `ConnectionIdleLifetime=2` + `ConnectionPruningInterval=1`: havuz kendi kendine saniyeler içinde
  boşalır.
- `MaxPoolSize` **düşürülmemeli**. 10'a çekmek suite'i kilitledi: bir test 3 WebApplicationFactory
  çalıştırabiliyor, her biri `min(çekirdek×5, 20)` Hangfire worker'ı açıyor, ~60 thread aynı
  connection string'in havuzunu paylaşıyor. `EmailSignalTests` tek bir testte 11 dakika %155 CPU'da
  döndü, Postgres tamamen boştaydı. Sorun havuzun büyüklüğü değil, hiç boşalmamasıydı.
- Npgsql `ConnectionIdleLifetime`'ın `ConnectionPruningInterval`'dan küçük olmasını reddeder.
- Redis'in 16 numaralı veritabanı yetmez (caller sayısı test sayısı kadar); 128'e çıkarıldı, index
  sarmalanıyor ve devralınan veritabanı hand-out sırasında `FLUSHDB` ediliyor.

**Hangfire:** `WorkerCount` ve `ShutdownTimeoutSeconds` config'e bağlandı, **production varsayılanları
değişmedi**. Test assembly'si bir `ModuleInitializer` ile ortam değişkeni olarak `WorkerCount=1`,
`ShutdownTimeoutSeconds=5` veriyor (ASP.NET ortam değişkenlerini varsayılan olarak okur, böylece 17
test dosyasının hiçbirine dokunmadan koşudaki ~200 host'un hepsine ulaşır). Bu, teardown'daki
`WaitForShutdownAsync` kaynaklı `TaskCanceledException`'ları bitirdi. Not: `ShutdownTimeout`'un
15s→30s çıkarılması daha önce aynı sorun için denenmişti ve işe yaramamıştı — beklemeyi uzatıyor,
iş yükünü azaltmıyordu. Asıl kaldıraç worker sayısı.

## Yerel test-host çökmesi: hâlâ teşhis edilmedi, CI'ı etkilemiyor (2026-09-03)

**Durum:** Yerel `dotnet test` koşularının bir kısmı ortada `Test host process crashed` ile kesiliyor.
4 ardışık koşunun 2'si böyle bitti (47 ve 72. testte); tamamlanan koşular ise her seferinde tam
olarak 107/107 ve 91 saniye. Yani yavaşlama değil, ani bir olay.

**Kritik ayrım — CI'da bu sorun yok.** Son 15 CI koşusunun **15'i de başarılı**. GitHub'daki tek
sorun süreydi (7.4 dk, bunun 411s'i `dotnet test`), asılma veya çökme değil. Çökme yalnızca
macOS + podman ortamında görülüyor ve yeni değil — proje hafızasında zaten "nadir açıklanamayan
test-host çökmesi" olarak duruyordu.

**Elenen sebepler** (bir dahaki sefere aynı yollar tekrar denenmesin):

- **Bellek değil.** Çökme anında sistem belleğinin %83'ü boş, swap kullanımı sıfır, macOS jetsam
  kaydı yok.
- **Yetim süreç değil.** Kesilen koşulardan kalan `testhost` süreçleri CPU yiyip sonraki koşuları
  yavaşlatıyor (`pkill -f "dotnet test"` yalnızca sarmalayıcıyı öldürür, çocuğu bırakır) — ama
  hepsi temizlenmiş bir makinede de çökme tekrarlandı.
- **Hangfire shutdown değil.** Worker sayısı 1'e indirildikten ve teardown hataları tamamen
  bittikten sonra da çökme devam etti. Bunlar iki ayrı sorun.
- **Container yükü değil.** 123 container'dan 2'ye inildikten sonra da sürüyor.
- macOS hiçbir crash raporu üretmiyor, yani sert bir native çökme imzası da yok.

**Sıradaki adım (yapılmadı):** `dotnet test --blame-crash` ile çökene kadar koşup dump almak. CI'a
faydası olmadığı, yalnızca yerel geliştirme deneyimini etkilediği için şimdilik ertelendi.

---

## Kabul edilen faker riski kapatıldı: paket ağaçtan stub ile çıkarıldı (2026-09-03)

**Bağlam:** Kullanıcı "GitHub'da hiçbir kritik uyarı kalmasın" dedi. Geriye tek açık Dependabot
uyarısı kalmıştı: `@faker-js/faker` 5.5.3 (high, `helpers.fake` üzerinden arbitrary code execution),
`postman/package-lock.json` içinde. 2026-09-02'de bilinçli olarak kabul edilmişti (yukarıdaki kayıt).

**Neden yükseltme mümkün değil (yeniden doğrulandı):** `openapi-to-postmanv2@6.3.3` ve
`postman-collection@5.3.1` ikisi de npm'deki **en güncel** sürüm — bekleyen bir upstream düzeltmesi
yok. `postman-collection` faker'ı tam `5.5.3`'e sabitliyor ve `lib/superstring/dynamic-variables.js`
içinden v8'de kaldırılmış API'yi (`faker.address.city` vb.) doğrudan çağırıyor; yamalı sürümü
(`10.5.0+`) zorlamak `npm run generate`'i `Cannot read properties of undefined (reading 'city')` ile
çökertiyor. Yani önceki kaydın analizi hâlâ geçerli.

**Karar:** Paketi yükseltmek yerine **ağaçtan çıkarıldı**. `postman/faker-stub/` altında, gerçek
`dynamic-variables.js`'ten üretilmiş, kullanılan **111 fonksiyonun tamamını** karşılayan bir yerel
paket duruyor; `package.json`'daki `overrides` ile `"@faker-js/faker": "file:faker-stub"` olarak
bağlandı.

**Neden güvenli:** faker orada yalnızca `{{$randomCity}}` türü Postman dinamik değişkenlerini
**istek anında** çözmek için var. Bizim ürettiğimiz koleksiyon bu değişkenlerden hiç içermiyor —
`scripts/generate-collection.js` OpenAPI şemasından somut örnek değerler basıyor. Yine de modül
`require` zamanında yükleniyor, bu yüzden bağımlılığı tamamen silmek değil, yerine bir stub koymak
gerekiyordu. Bir dinamik değişken bir gün kullanılırsa, makul görünen rastgele bir değer yerine
`stub:namespace.fn` gibi bariz sahte bir değer üretir — kasıtlı olarak görünür, sessiz değil.

**Doğrulama:** `npm ci` temiz kurulumda çalışıyor; `npm audit` → **0 vulnerability**. Gerçek faker
ile stub'ın ürettiği koleksiyonlar yapısal olarak karşılaştırıldı (47 istek; ad, metod, URL, gövde
ve header'lar birebir aynı), stub değeri çıktıya hiç sızmıyor (`grep -c "stub:"` → 0). Tek fark
`occurredAt` alanı, o da bizim kendi kodumuzun `new Date().toISOString()`'i — yukarıdaki düzeltme
notunda açıklanan, faker'dan bağımsız rastgelelik.

**Kurulum mekanizması — neden tarball:** İlk deneme `overrides` içinde `file:faker-stub` (dizin)
kullandı ve **CI'ı kırdı**. npm, `overrides` içindeki bir `file:` dizin yolunu proje köküne değil
**override edilen paketin dizinine göre** çözüyor: lockfile'a `node_modules/postman-collection/faker-stub`
yazılıyor ve o yol hiç var olmadığı için kırık bir symlink oluşuyor. npm 11 (yerel geliştirme
makinesi) bunu tolere ediyor, **npm 10 (CI'ın Node 22'si) etmiyor** —
`Cannot find module '@faker-js/faker/locale/en'` ile patlıyor. Bu yüzden stub bir **tarball** olarak
kuruluyor (`file:faker-js-faker-99.0.0-stub.tgz`): tarball link'lenmek yerine açılıyor, dolayısıyla
her iki npm sürümünde de aynı şekilde çözülüyor. Node 22/npm 10.9.8 ve Node 26/npm 11.19.0'da
`npm ci` + `npm run generate` ile ayrı ayrı doğrulandı.

**Ders:** Yerelde çalışması yetmiyor — bu değişiklik yerelde temiz klonda bile geçti, CI'da kırıldı.
Node/npm sürümüne duyarlı paket çözümleme değişiklikleri, CI'ın kullandığı sürümde
(`node:22-alpine` container'ı) doğrulanmalı.

**Bakım notu:** Stub, `postman-collection@5.3.1`'in çağrı yüzeyinden üretildi.
`faker-stub/` altındaki kaynağı düzenlemek tek başına **etkisizdir** — kurulan şey tarball'dır;
`npm run pack:faker-stub` ile yeniden paketleyip `.tgz`'yi de commit etmek gerekir. O paket yükseltilirse
`grep -oE "faker\.[a-zA-Z]+\.[a-zA-Z]+" node_modules/postman-collection/lib/superstring/dynamic-variables.js`
ile yüzey yeniden çıkarılıp stub güncellenmeli — eksik bir fonksiyon `undefined is not a function`
olarak patlar, sessizce yanlış çıktı üretmez.

---

# Spec dokümanındaki küçük tutarsızlıklar (bilgi amaçlı, aksiyon gerektirmiyor)

- Bölüm numaralandırması §32'den sonra §35, sonra §34, sonra §36 şeklinde
  karışık — muhtemelen yazım sırasında sıralama değişmiş.
- §30 sıralaması GDPR'ı KVKK'dan önce listeliyor; Türkiye-first
  pozisyonlamayla tutarlı olması için KVKK önce değerlendirilebilir (hukuki
  görüş gerektirir, bu doküman hukuki tavsiye değildir).
