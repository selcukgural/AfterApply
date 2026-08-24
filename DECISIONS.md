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

## Spec dokümanındaki küçük tutarsızlıklar (bilgi amaçlı, aksiyon gerektirmiyor)

- Bölüm numaralandırması §32'den sonra §35, sonra §34, sonra §36 şeklinde
  karışık — muhtemelen yazım sırasında sıralama değişmiş.
- §30 sıralaması GDPR'ı KVKK'dan önce listeliyor; Türkiye-first
  pozisyonlamayla tutarlı olması için KVKK önce değerlendirilebilir (hukuki
  görüş gerektirir, bu doküman hukuki tavsiye değildir).
