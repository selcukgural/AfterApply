# e-kariyerim — Development Plan

Kaynak: `ekariyerim-intelligence-platform-plan.md` (product/technical spec).
Bu doküman, spec'teki Phase (§22) ve Sprint (§23-27) tanımlarını uygulanabilir,
sıralı bir yol haritasına dönüştürür. Spec'te tanımlanmayan iki boşluk
(CSV Import ve Hardening/Launch sprintleri) burada eklendi — bkz. DECISIONS.md.

Kural: Her sprint'in sonunda "Definition of Done" MVP kapsamını (§4.2)
aşmayan, çalışan bir dilim olmalı. Sıradaki sprint'e geçmeden önce testler
ve README güncel olmalı (spec §31.6, §31.17).

---

## Sprint 0 — Foundation (spec Phase 1 / First Sprint)

**Amaç:** Henüz hiçbir domain feature'ı yok; sadece iskelet.

- Solution structure (bkz. DECISIONS.md — layer-first mi module-first mi)
- Clean Architecture katmanları + dependency-direction testleri (NetArchTest)
- Docker Compose dev environment (API + PostgreSQL)
- EF Core + PostgreSQL bağlantısı (henüz entity yok)
- Configuration (appsettings + secrets, hard-code yok — spec §31.9)
- Health checks (`/health`)
- Structured logging (Serilog önerilir)
- Test projeleri (xUnit + FluentAssertions, Testcontainers.PostgreSql altyapısı)
- README (çalıştırma komutları)

**DoD:** `docker compose up` ile API ayağa kalkar, `/health` 200 döner, boş test
projesi CI'da (varsa) yeşil geçer.

---

## Sprint 1 — Identity + Core Domain (spec Phase 2 + Phase 3, Second Sprint)

- User: kayıt, login, logout, profil (auth yaklaşımı → DECISIONS.md)
- Company, Job, Application, ApplicationEvent, ApplicationStatusHistory domain modelleri
- İlk migration seti
- Application CRUD API + status transition endpoint
- Temel validation (FluentValidation önerilir)

**DoD:** Postman/Swagger üzerinden bir kullanıcı kayıt olup application
oluşturabilir, status değiştirebilir, timeline event'i otomatik yazılır.

---

## Sprint 2 — Web UI MVP (spec Phase 4, Third Sprint)

- Login/Register ekranı
- Dashboard (§4.2 sayaçları: Total/Active/Waiting/Interviews/Offers/Rejected/Ghosted)
- Application list: filtre, arama, sıralama, pagination
- Application create/edit, detail, timeline görünümü
- Status yönetimi (UI'dan status değiştirme)

**Not:** Frontend framework kararı bekleniyor (DECISIONS.md). Backend'i
yavaşlatmaması ilkesi (§2 Backend First) gereği bu sprint'e kadar
ertelenebilir.

---

## Sprint 3 — Personal Analytics (spec Phase 5, Fourth Sprint)

- response/interview/offer/rejection/ghosting rate
- average + median response time
- Dashboard'a chart entegrasyonu

**DoD:** §9'daki tüm metrikler gerçek veriden hesaplanıp API + UI'da gösterilir.

---

## Sprint 4 — CSV Import (spec Phase 6)

> Spec'in Sprint listesinde (§23-27) bu sprint açıkça yoktu ama §4.2 MVE
> gereksinimi. Sprint 5'ten (LinkedIn import) önce buraya eklendi çünkü
> LinkedIn import'un dedup/normalize altyapısını CSV import kurar.

- CSV upload endpoint + boyut/format limiti
- Parser + column mapping
- Validation + hata raporu
- Duplicate detection (§8: Source+ExternalId → Job URL → Company+Title+AppliedAt → fuzzy)
- Import summary + idempotency

**DoD:** Aynı CSV iki kez yüklendiğinde ikinci yüklemede 0 yeni kayıt oluşur.

---

## Sprint 5 — LinkedIn Data Export Import (spec Phase 7, Fifth Sprint)

- ZIP upload + extraction
- `Job Applications*.csv` dosyalarının keşfi
- Parse + normalize + company/job resolution
- Sprint 4'teki dedup pipeline'ının reuse edilmesi
- Import summary (§4.2 örnek format)

**DoD:** "Wow moment" (§4.5) doğrulanır — gerçek bir LinkedIn export'u
yüklenip dakikalar içinde dolu bir dashboard oluşur.

---

## Sprint 6 — Reminders (spec Phase 8'in MVP alt kümesi)

> Phase 8'in tamamı (email vs.) MVP değil; sadece §4.2'deki "Reminder"
> gereksinimi burada karşılanıyor.

- Background job altyapısı (Hangfire vs native → DECISIONS.md)
- Follow-up reminder (N gün cevapsız → hatırlatma)
- "Possibly Ghosted" önerisi (configurable `GhostingThresholdDays`, default 30)

**DoD:** Cevapsız bir application, threshold'u geçtiğinde kullanıcıya
(in-app bildirim yeterli, email zorunlu değil) öneri düşer.

---

## Sprint 7 — MVP Hardening & Private Beta (yeni — spec'te yok)

> MVP'yi gerçek kullanıcıya açmadan önce spec §30'un (privacy/legal) ve
> §28'in (product metrics) kod tarafına dökülmesi gerekiyor; spec bunu bir
> sprint olarak tanımlamamış.

- Account deletion + personal data export (§4.2, §30)
- Privacy policy + explicit consent akışı
- Rate limiting, upload boyut/güvenlik limitleri (zip bomb koruması dahil)
- Product metrics instrumentation (§28: activation, engagement, retention)
- Deployment (Docker → seçilen cloud, DECISIONS.md)
- KVKK/GDPR self-review (hukuki onay değil, mühendislik checklist'i)

**DoD:** Bu sprint bitince spec §4.4'teki MVP başarı kriteri uçtan uca,
prod-benzeri bir ortamda doğrulanabilir durumda.

---

## Sprint 8+ — Yeniden planlama (2026-08-25): ilk yayın MVP değil, tam ürün

> Kullanıcı kararı: uygulama bir süre daha yayına alınmayacak (başka işler
> nedeniyle), bu yüzden ilk canlı sürüm artık kademeli bir MVP değil,
> aşağı yukarı bitmiş bir ürün olacak. Bu, spec §22 Phase 9-13'ün "MVP
> çıksın → gerçek kullanıcı gelsin → sonra ekle" sıralama mantığını
> değiştiriyor: zaman baskısı yok, yayın öncesi daha fazla faz bitirilebilir.
>
> **Değişmeyen bir kısıt var:** Phase 10 (Company Intelligence) ve §14
> (Candidate Experience Score), doğaları gereği **başka kullanıcıların**
> agregat verisine muhtaç (§15 confidence threshold'ları — <20 başvuru
> "Hidden"). Hiç kullanıcı olmadan bu iki faz gerçek anlamda "bitmiş"
> olamaz. Kullanıcıyla netleştirildi: bu fazların **altyapısı** (aggregation
> pipeline, confidence hesaplama, testler) şimdi kurulur, ama **aktivasyonu**
> (public/aggregate görünüm) gerçek veri eşiği geçilene kadar bir
> feature-flag ile kapalı tutulur. Monetization (§18) ise hâlâ ertelendi —
> spec'in "önce PMF doğrulanmalı" gerekçesi kabul edildi, ilk yayın ücretsiz.

Phase 9 (Email Integration — sadece Gmail) tamamlanmıştı, ama CASA
maliyeti/bürokrasisi yüzünden kullanıcıdan gizlendikten sonra 2026-08-31'de
koddan tamamen kaldırıldı; yerine Cloudflare Email Routing tabanlı Forwarding
yönü kaldı (bkz. `DECISIONS.md`'nin ilgili girdileri). Kalan fazlar şu
sırayla planlanıyor:

---

## Sprint 8 — AI Job Matching (spec Phase 11)

> **Durum (2026-08-25): backend + frontend implementasyonu tamamlandı**
> (unit testler yeşil — bkz. DECISIONS.md "Sprint 8 kararları ve bulguları").
> **Podman entegrasyon testleri (2026-08-26): `MatchingTests.cs` dahil
> Sprint 8-11 suite'inin tamamı (58 test) yeşil.** Bekleyen: gerçek
> `OpenAI:ApiKey` ile manuel smoke test.

> **Kullanıcıdan gizlendi (2026-08-29):** Bu faz, kullanıcının CV metnini
> OpenAI'a (ABD, yurt dışı) gönderiyor — bu, `PRIVACY_CHECKLIST.md`'nin
> "Avukata götürülecek envanter ve eksikler" bölümünde işaretlenen en
> kritik KVKK açığı (granüler rıza + yurt dışı aktarım disclosure'ı
> yok, kullanıcının bir avukatı yok). Bu yüzden Sprint 10/11'deki
> `CompanyIntelligence:Enabled` paterni tekrarlanarak backend/frontend
> tüm uç noktalar bir config flag ile kullanıcıdan tamamen gizlendi —
> kod silinmedi, sadece erişilemez hâle getirildi. Yeniden açılması,
> gerekli Aydınlatma Metni/Açık Rıza/yurt dışı aktarım disclosure'ı
> hazır olduktan sonraya ertelendi (ne zaman ele alınacağı ayrı bir
> karar — bkz. DECISIONS.md).

> **Yeniden açıldı (2026-09-01):** `PRIVACY_CHECKLIST.md`'nin #2 (granüler
> açık rıza) ve #3 (yurt dışı aktarım disclosure'ı) maddeleri bu özelliğe
> özgü olarak kapatıldı — `/privacy` sayfasına OpenAI'ı isimlendiren yeni
> bir "Yurt dışına veri aktarımı" bölümü, ve Ayarlar'daki CV kaydetme
> formuna genel kayıt onayından bağımsız, her kaydette yeniden işaretlenen
> bir onay kutusu eklendi (bkz. DECISIONS.md). `Matching:Enabled` `true`'ya
> çekildi. **Bu tam bir hukuki inceleme değil** — KVKK m.10 tam formatı,
> VERBİS muafiyet teyidi, Çerez Politikası ve ToS hâlâ açık kalemler.

> Diğer data-gated fazlardan farklı olarak bu faz **tek kullanıcının kendi**
> CV'si + job description'ına dayanıyor, başka kullanıcı verisine bağımlı
> değil — dolayısıyla yayın öncesi tam olarak bitirilebilir.

> **Kaldırıldı (2026-09-02):** Bu faz — backend (`Matching` modülü:
> `CandidateProfile`/`JobMatch` entity'leri, `IJobMatchingProvider`/
> `OpenAiJobMatchingProvider`, `MatchingEndpoints`, `Matching:Enabled`
> flag'i), frontend (`JobMatchPanel`, Ayarlar'daki "CV / Profile" bölümü)
> ve `/privacy` sayfasındaki OpenAI'a özel "Yurt dışına veri aktarımı"
> disclosure'ı dahil — koddan tamamen kaldırıldı; DB tabloları yeni bir
> `RemoveJobMatching` migration'ıyla drop edildi. Ürün kararı: kullanıcının
> CV'sini OpenAI'a göndererek puanlama yapan bu özellik ürün kapsamından
> tamamen çıkarıldı. **OpenAI entegrasyonu koddan kaldırılmadı** — gelen
> e-postaları sınıflandırmak için kullanılan ayrı `EmailIntegrations`
> özelliği (`OpenAiEmailClassificationProvider`/
> `OpenAiEmailJobExtractionProvider`, paylaşılan `OpenAiOptions`, artık
> `AfterApply.Infrastructure.OpenAi` altında) olduğu gibi duruyor. Bkz.
> DECISIONS.md'nin ilgili girdisi.

- Yeni bir profil/CV modülü: kullanıcı CV/skill bilgisini girer (format —
  düz metin mi, dosya upload+parse mı — sprint başında DECISIONS.md'de
  netleştirilecek, OPEN)
- AI provider seçimi — OPEN, sprint başında karara bağlanmalı
  (`claude-api` skill'i model/fiyat karşılaştırması için kullanılabilir)
- Job description girdisi: mevcut `Job.Description` (LinkedIn import'ta
  dolu olabiliyor) veya kullanıcının elle yapıştırdığı metin
- Matching endpoint: CV + job description → AI provider → Score/Strong
  Match/Missing/Recommendation (spec §12 örnek format)
- Sonuç persist mi ediliyor yoksa her seferinde yeniden mi hesaplanıyor →
  OPEN
- LLM çağrısı ücretli — rate limiting (mevcut `upload` policy paterni
  reuse edilebilir) + maliyet kontrolü

**DoD:** Kullanıcı profilini bir kez girer, herhangi bir application/job
için "Match Score" hesaplatabilir, spec §12'deki formatta sonucu görür.

---

## Sprint 9 — Browser Extension (spec Phase 12)

> **Durum (2026-08-25): tamamlandı, gerçek LinkedIn sayfasında manuel
> olarak uçtan uca doğrulandı.** Kullanıcı backend+frontend'i lokal
> çalıştırıp eklentiyi yükledi, gerçek bir LinkedIn ilanında "I Applied"
> ile başvuru oluşturdu — PAT auth (SmartBearer scheme forwarding), CORS
> (`host_permissions`), URL kalıbı (`/jobs/view/` + `currentJobId`), ve
> scraping (href-tabanlı title/company/location) canlı ortamda çalıştığı
> doğrulandı (bkz. DECISIONS.md "Sprint 9 kararları ve bulguları" — üç
> gerçek bug bu doğrulama sırasında bulunup düzeltildi). **Podman
> entegrasyon testleri (2026-08-26): `PersonalAccessTokenTests.cs`,
> `ExtensionApplicationTests.cs` dahil yeşil.** Bekleyen: sadece Chrome
> Web Store yayını (kapsam dışı, plan zaten böyle diyordu).

- Chrome/Edge extension scaffold (Manifest V3)
- LinkedIn job sayfasından scraping: company/title/URL/LinkedIn job
  id/location/description/published date
- Kimlik doğrulama: extension web session'ından bağımsız çalışmalı →
  yeni bir Personal Access Token (PAT) mekanizması gerekiyor (mevcut
  access/refresh JWT modeli extension için uygun değil — kısa ömürlü ve
  web `localStorage`'a bağlı). PAT tasarımı (üretim/iptal/scope) — OPEN
- "I Applied" butonu → mevcut `POST /api/applications` (Source=LinkedIn,
  ExternalId=job id) — Sprint 5'in `JobResolver`/dedup pipeline'ı reuse
  edilir, yeni bir import yolu icat edilmez
- Chrome Web Store'a yayınlama, gerçek launch'a bağlı ayrı bir adım — bu
  sprint sadece extension'ın kendisini teslim eder

**DoD:** Kullanıcı bir LinkedIn ilan sayfasında "I Applied" tıkladığında,
dashboard'da doğru company/job/status=Applied ile yeni bir application
görünür.

---

## Sprint 10 — Company Intelligence altyapısı (spec Phase 10 + §15), UI'da kapalı

> **Durum (2026-08-26): backend implementasyonu tamamlandı** (unit testler
> yeşil; podman entegrasyon testi `CompanyIntelligenceTests.cs` dahil
> Sprint 8-11 suite'inin tamamı yeşil, 2026-08-26). UI yok (plan zaten
> böyle diyordu — Sprint 10 sadece altyapı).

- Yeni `CompanyIntelligence` modülü: şirket bazlı aggregation (Applications,
  Response Rate, Ghosting Rate, Avg/Median Response Time, Interview Rate,
  Offer Rate) — tüm kullanıcılar üzerinden, anonim/aggregate
- §15 confidence bucket hesaplama (Hidden/Very Low/Low/Medium/High, spec'in
  başlangıç hipotezi: <20/20-49/50-199/200-999/1000+)
- `CompanyIntelligence:Enabled` config flag, appsettings-driven, varsayılan
  `false` (Sprint 4/7'deki config-driven limit paterni tekrarlanıyor,
  hard-code yok)
- Aggregation mantığı, flag kapalıyken de entegrasyon testleriyle
  (sentetik veri) doğrulanır; flag'in kendisi sadece endpoint/UI'ı
  gerçek trafiğe kapatır
- §16 fairness dili (veri-odaklı, tarafsız metin şablonları) bu sprintte
  uygulanır

**DoD:** Aggregation pipeline sentetik veriyle test edilip confidence
bucket'ların doğru hesaplandığı doğrulanır; flag kapalıyken hiçbir uç
noktadan company-level veri sızmaz.

---

## Sprint 11 — Candidate Experience Score altyapısı (spec §14), UI'da kapalı

> **Durum (2026-08-26): backend implementasyonu tamamlandı** (unit testler yeşil — bkz.
> DECISIONS.md "Sprint 11 kararları ve bulguları"). Endpoint-şekli OPEN'ı, mevcut
> `CompanyIntelligenceMetrics`e iki alan (`ClosureRate`, `CandidateExperienceScore`) eklenerek
> çözüldü — ayrı bir endpoint yok. **Podman entegrasyon testleri (2026-08-26): Sprint 8-11
> suite'inin tamamı (58 test, `CompanyIntelligenceTests.cs`'e eklenen yeni testler dahil)
> yeşil** — podman VM 2GiB→6GiB'ye çıkarıldı ve `TESTCONTAINERS_RYUK_DISABLED=true` ile
> koşuldu (rootless podman'da Ryuk'un socket-mount kısıtlaması, bkz. README).

> **Kapsam kararı (2026-08-26):** Spec §14'ün 5 alt metriğinden ikisi
> (Interview Experience, Process Transparency) için repo'da hiç ham veri
> yok — `ApplicationStatus`/`ApplicationStatusHistory` sadece durum
> geçişi + zaman damgası tutuyor, aday geri bildirimi veya red gerekçesi
> gibi bir alan yok. Spec'in kendisi de "İlk MVP'de score algoritması
> yapılmamalı, önce ham veri toplanmalı" diyor. Kullanıcıyla netleştirildi:
> bu sprint sadece veri kaynağı olan **3 alt metrikle** sınırlı;
> Interview Experience ve Process Transparency, ilgili ham veri toplanana
> kadar kapsam dışı kalıyor (ne zaman ele alınacağı ayrı bir karar).

- Composite score, sadece şu 3 alt metrikten (0-100 skalada):
  - **Responsiveness** → Sprint 10'un `ResponseRate`'i doğrudan reuse
    edilir
  - **Response Time** → `AverageResponseTimeDays`'ten normalize edilir
    (config-driven bir `ResponseTimeCapDays` eşiğine göre; cap'i aşan/aşkın
    süre = 0 puan). Hiç yanıt yoksa (`AverageResponseTimeDays == null`)
    bu alt metrik "veri yok" sayılır ve composite, kalan alt metriklerin
    ağırlıkları yeniden normalize edilerek hesaplanır — 0 puan
    *varsayılmaz* (0 puan "yanıt geldi ama çok geç" ile "hiç yanıt yok"
    ayrımını kaybeder)
  - **Closure Rate** → **Sprint 10'daki `TerminalApplicationStatuses`
    reuse edilmeyecek** (Ghosted'ı "kapanmış" sayıyor, oysa CES'in
    cezalandırması gereken tam olarak bu). CES'e özel yeni bir
    sınıflandırma: sadece şirketin açıkça bir sonuç bildirdiği durumlar
    (Rejected, Accepted) "closure" sayılır; Ghosted (şirket kapanış
    vermedi) ve Withdrawn (adayın kendi kararı, şirket sinyali değil)
    hariç tutulur
- Ağırlıklandırma: config-driven (Sprint 4/7/10'daki "hard-code yok"
  paterni), varsayılan eşit ağırlık (1/3 - 1/3 - 1/3); veri eksikse
  yukarıdaki gibi mevcut alt metrikler arasında yeniden normalize edilir
- Aynı `CompanyIntelligence:Enabled` flag'i altında (ayrı bir flag
  gereksiz — aynı aktivasyon koşuluna, gerçek veri hacmine bağlı), aynı
  confidence bucket (Sprint 10'un eşikleri) kullanılır
- Response route/response şekli (mevcut `/api/company-intelligence/
  {companyId}` yanıtına yeni bir alan mı, ayrı bir endpoint mi) —
  implementasyon başında DECISIONS.md'de netleştirilecek (OPEN, düşük
  riskli bir detay)

**DoD:** Sprint 10'daki gibi sentetik veriyle doğrulanan, flag kapalıyken
gizli bir skor hesaplama pipeline'ı; Ghosted/Withdrawn içeren senaryolarda
Closure Rate'in beklenen şekilde düştüğü/etkilenmediği testle kanıtlanır.

---

## Sprint 12 — kaldırıldı (eski: B2B Employer Dashboard iskeleti, spec Phase 13)

> **Karar (2026-08-26):** Roadmap'ten tamamen çıkarıldı — bkz. DECISIONS.md
> "Sprint 12 (B2B) — plandan çıkarıldı". Gerekçe: hiçbir gerçek işveren
> talebi/sinyali yokken şirket hesabı/doğrulama modeli tasarlamak bu
> aşamada spekülatif bulundu. Ürün önceliği B2C (iş arayan) tarafında
> kalıyor; bu fikir gerçek bir işveren talebi ortaya çıkarsa yeniden ele
> alınabilir — o zamana kadar aktif planlamanın bir parçası değil.

---

## Sprint 13 — Launch Hazırlığı v2

> **Kapsam kararı (2026-08-26):** Cloud provider kararı verildi (bkz.
> DECISIONS.md §5) — Azure/AWS/kendi VPS'i gibi ücretli seçenekler
> yerine kalıcı gerçek ücretsiz katmanlı, kanıtlanmış bir stack
> seçildi. Gerekçe: henüz gerçek trafik/ödeme yapan kullanıcı yok,
> paid altyapıya şimdiden yatırım yapmak projenin tekrar eden
> YAGNI/erken-optimizasyon-yapma prensibiyle çelişirdi.

> **Güncelleme (2026-08-26):** Kullanıcı Google Cloud'da 90 günlük/$300
> kredili bir deneme hesabı açtı ve tüm parçaların (Postgres, Redis, API,
> web) tek sağlayıcıda toplanmasını istedi — Neon/Upstash/Vercel'in
> yerini Cloud SQL/Memorystore/ikinci bir Cloud Run servisi aldı (bkz.
> DECISIONS.md §5 "Postgres + Redis + web de Google Cloud'a taşındı").
> **Önemli:** Cloud Run'ın aksine Cloud SQL/Memorystore'un kalıcı bir
> ücretsiz katmanı yok — sadece 90 gün/$300 kredi boyunca ücretsiz,
> sonrasında ~$45-55/ay gerçek bir maliyet oluşacak (detay DECISIONS.md).

- **Cloud provider — DECIDED, kod hazır:** Google Cloud tek sağlayıcı —
  Cloud Run × 2 (backend .NET API + frontend Next.js, ikisi de mevcut
  `Dockerfile`'ları kullanıyor), Cloud SQL for PostgreSQL, Memorystore
  for Redis (Basic tier, Direct VPC Egress). Cloud Run custom domain'de
  otomatik ücretsiz SSL sağlıyor — DEPLOYMENT.md'nin "no reverse
  proxy/TLS" notu bu şekilde kapanıyor, ayrı bir Caddy/Nginx'e gerek
  kalmıyor.
- **Redis — DECIDED:** planlama sırasında bulgu çıktı — kod tabanında
  Redis şu an hiçbir iş mantığı tarafından kullanılmıyor (rate limiting
  in-memory `FixedWindowLimiter` ile çalışıyor, sadece health check
  Redis'e bağlı). Buna rağmen Memorystore eklenmesine karar verildi —
  ileride cache/distributed rate-limiting ihtiyacı çıkarsa hazır olsun
  diye; bunun (Upstash'in aksine) artık gerçek bir aylık maliyeti var,
  bilinçli kabul edildi. **Geri alındı (2026-09-06):** o ihtiyaç hiç
  doğmadı ve eklenen `HybridCache` katmanı L2'yi fiilen hiç kullanmıyordu
  (her girdide `LocalCacheExpiration == Expiration`, backplane yok), bu
  yüzden Memorystore silinip cache in-process'e indirildi — bkz.
  DECISIONS.md "Redis kaldırıldı, cache in-memory'ye indi".
- **Error tracking — DECIDED, kod hazır (2026-08-26):** Sentry (.NET +
  Next.js ikisini de destekliyor, ücretsiz tier). `Sentry.AspNetCore`
  6.9.0 (backend, config-driven `Sentry:Dsn`, boşsa SDK kendini
  devre dışı bırakıyor) ve `@sentry/nextjs` 10.71.0 (frontend,
  `instrumentation.ts`/`instrumentation-client.ts`/`sentry.server.config.ts`/
  `sentry.edge.config.ts`) eklendi, her ikisi de `dotnet build` +
  `npm run build`'da doğrulandı. Detaylar: DECISIONS.md "Sprint 13
  kararları ve bulguları".
- Secrets: Cloud Run'ın entegre Secret Manager'ı (ücretsiz tier) —
  DEPLOYMENT.md'nin "no secrets manager" notu bu şekilde kapanıyor,
  `.env.prod` düz dosyası prod'da kullanılmıyor
- Migrations: `dotnet ef database update` adımı CI/CD'de (GitHub
  Actions) ya da manuel çalıştırılır — otomatik `Database.Migrate()`
  yok (Sprint 7 kararı korunuyor)
- CI/CD: `.github/workflows/deploy.yml` (eski `deploy-backend.yml`'in
  yerini aldı) — iki job, `deploy-backend` ve `deploy-web`, ikisi de
  Cloud Run'a Workload Identity Federation ile deploy ediyor (statik
  JSON key yok). `deploy-backend`'in `flags:`'ine
  `--add-cloudsql-instances=...`/`--network=default`/`--subnet=default`
  eklendi (Cloud SQL + Memorystore bağlantısı için). `push: main`'de
  otomatik ama seçici deploy oluyor (2026-09-01) — `dorny/paths-filter`
  ile hangi taraf değiştiyse sadece o job çalışıyor; `workflow_dispatch`
  ise `target` input'uyla (`backend`/`web`/`both`) manuel/zorunlu
  redeploy için duruyor. DEPLOYMENT.md "Sprint 13: real cloud deployment"
  bölümünde hesap kurulumundan ilk deploy'a kadar tüm adımlar var.
- Son privacy/legal review (ToS, KVKK/GDPR self-review — hukuki onay
  gerektirir, bu doküman hukuki tavsiye değildir) — sadece checklist
  maddesi olarak tutuluyor, bu planda detaylandırılmıyor
- ~~Domain/branding finalize~~ — tamamlandı (2026-08-26): `ekariyerim.com`
  (Cloudflare'den), domain mapping kuruldu, SSL sertifikası provisioning
  aşamasında (`DomainRoutable: True` doğrulandı, "CertificatePending" —
  15 dk-birkaç saat içinde tamamlanması bekleniyor)
- ~~Sprint 8-11'in tüm entegrasyon testleri~~ — tamamlandı (2026-08-26, 58/58 yeşil)
- ~~Uçtan uca manuel smoke test~~ — tamamlandı (2026-08-26): gerçek
  `ekariyerim` GCP projesinde `afterapply-api`/`afterapply-web` deploy
  edildi, `/health` → 200 Healthy (Postgres+Redis), kayıt akışı → 201 +
  JWT. Süreçte 4 gerçek bulgu çıktı ve düzeltildi (bkz. DECISIONS.md
  "Sprint 13 — gerçek deploy"): Cloud Run varsayılan private (elle
  `allUsers`/`run.invoker` verildi), migration'lar ilk deploy'da
  unutulmuştu (geçici authorized-networks ile çalıştırıldı), Secret
  Manager'da uzun komutların kopyala-yapıştırda bozulması (runbook'a
  kopyala butonu + `--data-file` paterni eklendi)

**DoD:** ✅ **Karşılandı (2026-08-26).** Google Cloud üzerinde (`ekariyerim`
projesi, Cloud Run × 2 + Cloud SQL + Memorystore) gerçek bir dağıtım canlı;
custom domain (`ekariyerim.com`) mapping'i kuruldu, SSL provisioning
aşamasında; kayıt akışı uçtan uca doğrulandı (201 + JWT). Sprint 8-11'in
DoD'leri bu ortamda ayrıca doğrulanmadı (kapsamı: sadece temel akış smoke
test edildi) — flag'leri kapalı olan Sprint 10/11 özellikleri (Company
Intelligence, Candidate Experience Score) hâlâ gerçek trafik/veri
bekliyor, bu Sprint 13'ün kapsamı dışında.

---

## Kullanıcıya kapalı / ulaşmayan özellikler — envanter ve açma sırası (2026-09-07)

> **Bağlam:** Ürün canlı ama gerçek kullanıcı yok. Bu envanter, "yazılmış,
> çalışır durumda, ama kullanıcıya ulaşmayan" her şeyi tek yerde topluyor ve
> hangi sırayla ele alınacağını kaydediyor. Sıralama ilkesi: **kilidi bizde
> olan iş önce.** Bir maddenin kilidi veri hacmi, hukuki görüş veya dış onaysa,
> kod hazır olsa bile sıraya sonra girer — 15 sprinttir yapılan hata tam olarak
> kilidi bizde olmayan işi kodla açmaya çalışmaktı.

Sıra numarası = ele alınma sırası. Madde numarası (K1-K6) = kalıcı kimlik.

**Envanterin durumu (2026-09-09):** K5, K3, K4 ve K6 kapandı. Açık kalan iki madde
K2 ve K1. **K1'in kilidi tamamen dışarıda** (veri hacmi + hukuki görüş); **K2'ninki
karışık** — ölçülecek veri dışarıda, ama açma eşiği, kullanıcı metni ve kullanıcı
bazlı ayar bizde (bkz. K2'nin "Açma koşulu" bölümü). Yani bugün kodla açılabilecek
bir madde yok, ama K2'nin hazırlığı veri beklemiyor; sıranın gövdesi V0-V5'te.

### Sıra 1 — K5: Ürün metriklerini görünür kılmak ✅ (2026-09-07)

- **Ne var:** `ProductMetricsService` her gün aktivasyon oranı, WAU, D7/D30/D90
  tutunma, 30 günlük başvuru ve durum-değişimi sayısını hesaplıyor
  (`Program.cs`, `product-metrics-snapshot` recurring job, `Cron.Daily()`).
- **Neden ulaşmıyor:** Çıktı tek bir `LogInformation` satırı. Endpoint yok,
  sayfa yok, uyarı yok.
- **Kilidi:** Bizde. Dış bağımlılık yok.
- **Neden ilk:** Diğer beş maddenin hiçbirine bu sayılar olmadan dürüst karar
  verilemez. Ölçmeden açılan her flag tahmin olur.

### Sıra 2 — K3: Başvuruya elle olay ekleme ✅ (2026-09-07)

- **Ne var:** `POST /api/applications/{id}/events` canlı ve testli;
  `applicationsApi.addEvent` (`web/src/lib/api/applications.ts:68`) yazılmış.
  Olay tipleri: `RecruiterContacted`, `ScreeningStarted`, `InterviewScheduled`,
  `InterviewCompleted`, `OfferReceived`, `FollowUpSent`, `StatusChanged`.
- **Neden ulaşmıyor:** `addEvent`'i çağıran hiçbir bileşen yok ve `GET .../timeline`'ın
  client metodu bile yazılmamış. Kullanıcı elle kayıt düşemiyor.
  (Düzeltme 2026-09-07: `ApplicationEvents` tablosu **boş değil** — `Application.Create`
  bir `ApplicationCreated`, `ChangeStatus` da her geçişte bir `StatusChanged` olayı yazıyor.
  Yani sistem olayları zaten birikiyor; eksik olan yalnızca kullanıcının kendi ekleyebildiği
  olaylar. Birleşik zaman çizelgesi bu iki sistem tipini eliyor, yoksa her durum değişimi
  listede iki kez görünür.)
- **Kilidi:** Bizde — eksik olan tek şey UI.
- **Ek fayda:** Sprint 11'de "ham veri yok" diye kapsam dışı bırakılan CES alt
  metrikleri (Interview Experience, Process Transparency) tam olarak bu olay
  kayıtlarını istiyordu. K1'in eksik yarısını bu madde üretmeye başlıyor.

### Sıra 3 — K4: Şirket zenginleştirme verisini yüzeye çıkarmak ✅ (2026-09-07)

- **Ne var:** Hangfire job'ı LinkedIn şirket sayfasından `Website`, `Industry`,
  `Country` çekiyor; `KariyerNetUrl` de saklanıyor (`Company.EnrichFrom`).
- **Neden ulaşmıyor:** API sözleşmesine yalnızca `companyWebsite` ve
  `companyLinkedInUrl` çıkıyor. `Industry`, `Country`, `KariyerNetUrl` hiçbir
  yanıtta yok.
- **Kilidi:** Bizde.
- **Neden K1'den önce:** `Industry`/`Country`, şirket adı vermeyen sektör/ülke
  bazlı toplu raporun kırılım eksenleri. K1'in aksine örneklem eşiği istemez,
  yani bugünkü veriyle bile anlamlı çıktı üretir.

> **Paket tamamlandı (2026-09-07).** K5+K3+K4 tek bir hazırlık paketi olarak yapıldı:
> `ProductMetricsDailySnapshots` tablosu + gün başına tek satıra upsert eden, config'ten okunan
> (`Metrics:SnapshotCronExpression`, varsayılan 30 dakikada bir) Hangfire işi; `Users.IsAdmin`
> kolonuyla korunan
> `GET /api/admin/metrics` ve `/admin/metrics` sayfası (yetki config'te değil veritabanında:
> config süreç açılışında okunuyor, dolayısıyla admin ekleme/çıkarma redeploy isterdi — kolondan
> okuyunca bir sonraki istekte geçerli oluyor, bkz. DEPLOYMENT.md §3a); başvuru detayında durum geçmişi ile
> elle eklenen olayları birleştiren tek "Süreç" listesi ve "Olay ekle" formu; şirketin
> `Industry`/`Country`/`KariyerNetUrl` alanlarının detay yanıtına ve ekrana taşınması.
> Yol boyunca iki gerçek bulgu çıktı: (1) `ApplicationEvents` boş değilmiş — sistem
> `ApplicationCreated`/`StatusChanged` yazıyor, birleşik liste bunları eliyor; (2) `Metadata`
> **jsonb** kolonu, düz metin not 500'e düşüyordu — not artık `{"note":"..."}` olarak gidiyor ve
> `CreateEventRequestValidator` geçersiz JSON'ı 400'le reddediyor.

### Sıra 4 — K2: E-posta önerilerinin otomatik onaylanması (bugün gölge modda)

- **Ne var:** `EmailForwardingService.TryAutoApplyAsync` +
  `EmailAutoApprovalOptions`. Ayarlar: `Enabled=false`,
  `ShadowModeEnabled=true`, `ConfidenceThreshold=0.9`.
- **Bugünkü davranış:** Nitelikli öneriler "would auto apply" diye yalnızca
  loglanıyor, hiçbir şey değiştirilmiyor. Kullanıcı her öneriyi elle onaylıyor.
- **Kilidi:** Karışık — ölçülecek **veri** dışarıda, ama açma eşiği, kullanıcı
  metni ve kullanıcı bazlı ayar bizde. Ayrıntısı aşağıda "Açma koşulu"nda.
- **Düzeltme (2026-09-07):** Burada "gölge kararları sorgulanabilir hale
  getirelim" yazıyordu; **gereksizmiş.** `EmailSuggestions` tablosu zaten
  `ConfidenceScore`, `MatchedRule`, `MatchType` ve kullanıcının nihai kararını
  (`Status`) tutuyor, ve auto-apply'ın nitelikli-olma koşulu tamamen bu
  kolonların fonksiyonu. Yani doğruluk analizi geriye dönük, istenen her eşik
  için hesaplanabilir. Gölge log satırı tablodan *daha azını* kaydediyor
  (`SuggestionId`'yi bile yazmıyor).

**Yapıldı (2026-09-07) — bayrak hâlâ kapalı, ama artık açılabilir hale geldi:**

1. **Tek tıkla geri alma.** `POST /api/email-forwarding/suggestions/{id}/revert`
   + Bildirimler sayfasında "Geri al". Durumu auto-apply öncesine döndürür;
   kullanıcı o başvuruyu kendisi ilerletmişse `409` ile reddeder (yoksa
   kullanıcının kendi değişikliğini sessizce silerdi). Yeni
   `EmailSuggestionStatus.Reverted` ve `StatusChangeOrigin.EmailAutoApplyReverted`
   — ikisi de string olarak saklandığı için migration gerekmedi.
   **Asıl gerekçe kolaylık değil ölçüm:** yanlış olmanın maliyetini "kaydım
   bozuldu"dan bir tıka indiriyor, ve geri alma oranı aradığımız doğruluk
   metriğinin ta kendisi.
2. **Kalibrasyon yüzeyi.** `GET /api/admin/auto-approval-calibration` +
   `/admin/metrics` sayfasında güven aralığı bazlı tablo.
   `Reverted / (AutoApplied + Reverted)` okunacak sütun; `Confirmed / (Confirmed
   + Dismissed)` de gösteriliyor ama **yanıltıcı** — kullanıcıya *gösterilip
   sorulmuş* önerileri ölçüyor, sorulmadan yapılanları değil. Bu sapma veri
   biriktikçe küçülmüyor.

**Açma koşulu — kilidin dört parçası (2026-09-09'da ayrıntılandırıldı).**
"Kilidi dışarıda" doğru ama eksikti: dördün üçü bizde, yalnızca biri veri bekliyor.

*Nitelikli olma kuralı (bugünkü hâliyle):* bir öneri ancak üç koşulu **birden**
sağlarsa auto-apply adayı — `MatchType == DomainMatch` (eşleşmesiz "yeni ilan"
önerileri hiç aday değil), `MatchedRule` `Llm:` ile başlıyor (kural tabanlı skor
"elle ayarlanmış bir ağırlık, kalibre edilmiş bir olasılık değil" diye bilerek
dışarıda) ve `ConfidenceScore >= ConfidenceThreshold`. Prod'da OpenAI anahtarı
bağlı, yani veri akmaya başladığı an bu yol çalışır durumda.

1. **Veri — dışarıda.** Tek gerçek dış bağımlılık.
   `GET /api/admin/auto-approval-calibration` (`/admin/metrics`) `DomainMatch + Llm:`
   satırlarını 0.5/0.6/0.7/0.8/0.9/0.95/1.0 bantlarına bölüyor; bantların üst ucu
   ince, çünkü karar tam olarak "0.9 mu 0.95 mi" sorusu. Bugün o tablo boş.
2. **Hangi sayının yeterli olduğu — bizde, ve hâlâ yazılmadı.** "Anlamlı sayıda
   auto-apply ve düşük geri alma oranı" bir eşik değil, bir temenni. Karara
   bağlanması gerekenler: üst banttaki minimum örneklem (n), kabul edilebilir
   maksimum geri alma oranı, izleme süresi. **Bu, veri gelmeden yapılmalı** —
   sonra yapılırsa eşiği sayıya bakarak seçmiş oluruz, ki o ölçüm değil
   rasyonalizasyondur.
3. **Hangi sütunun okunacağı — çözülmüş, ama iki aşamalı.** `AgreementRate`
   (`Confirmed / (Confirmed + Dismissed)`) bayrak kapalıyken elimizdeki **tek**
   sinyal ve yapısal olarak iyimser: kullanıcıya *gösterilip sorulmuş* önerileri
   ölçüyor, sorulmadan yapılacakları değil, ve bu sapma veri biriktikçe küçülmüyor.
   Aradığımız metrik `RevertRate` (`Reverted / (AutoApplied + Reverted)`) ancak
   bayrak açıldıktan sonra doluyor. Yani K2 tek adımlı değil: agreement'a bakıp
   **temkinli** aç → revert oranını izle → eşiği oynat ya da geri kapat. Açmak
   deneyin kendisi, sonucu değil.
4. **Kullanıcıya söylediğimizle çelişki — bizde, ve K6'nın aynısı.** Bugün her
   yüzeyde "öneri, siz onaylarsınız" yazıyor: `help.gmailScanning` ("Bir öneriyi
   onaylayın ya da reddedin"), gizlilik ("gözden geçireceğiniz bir öneriye dönüşmek
   üzere"), eklenti tanıtım metni ("onaylayacağınız ya da yok sayacağınız bir
   öneriye çeviriyor"). Bayrak açıldığı an bu cümleler yanlış olur — ürün, başvuru
   durumunu sormadan değiştiriyor olur. **Sıra K6'daki gibi: önce metin, sonra
   bayrak.**

**Buna bağlı, daha önce hiç yazılmamış eksik:** `EmailAutoApproval` **global bir
config bayrağı**, kullanıcı başına tercih yok. Bugün açılırsa Gmail Taraması'nı
açmış herkes için açılır, kimse istemeden. Ya bir kullanıcı ayarı gerekiyor (kod
işi, kilidi bizde) ya da metinlerin açıkça "otomatik uygulanır, tek tıkla geri
alabilirsiniz" demesi.

**Sıra:** eşik rakamlarını yaz → kullanıcı metinlerini (ve gerekirse kullanıcı
bazlı ayarı) hazırla → trafiği bekle → üst bantta n dolunca temkinli aç → revert
oranını izle. İlk iki adım bugün yapılabilir; yalnızca son üçü veri bekliyor.

### Sıra 5 — K1: Company Intelligence + Candidate Experience Score

- **Ne var:** `appsettings.json` → `CompanyIntelligence:Enabled=false`.
  `GET /api/company-intelligence/{companyId}` flag kapalıyken 404.
  Hazır hesaplama: şirket bazlı yanıt oranı, ghosting oranı, ortalama/medyan
  yanıt süresi, mülakat/teklif oranı, `ClosureRate`, bileşik
  `CandidateExperienceScore`, güven aralığı.
- **Neden ulaşmıyor:** İki ayrı sebep — (a) flag kapalı, (b) `web/src` içinde
  tek bir referansı yok, yani flag açılsa bile gösterecek ekran yok.
- **Kilidi:** Üçünden biri bizdeydi ve kapatıldı; ikisi hâlâ dışarıda:
  1. ~~**Eşik kararı**~~ — **yapıldı (2026-09-07).** Merdiven 20/50/200/1000'den
     **50/100/250/1000**'e çıkarıldı. 20 başvuru, tek bir kötü işe alım
     yöneticisinin her yüzdeyi birkaç puan oynatabildiği bir örneklem; şirket
     ise isimle anılıyor. Eşiği yükseltmek her zaman daha fazlasını gizler,
     yani güvenli; düşürmek adalet incelemesi ister — bunu bir unit test
     koruyor (`The_Shipped_Ladder_Never_Names_A_Company_Below_Fifty_Applications`).
  2. **Veri hacmi** — dışarıda. Kullanıcı gerekiyor.
  3. **Hukuki görüş** — dışarıda. KVKK + itibar hukuku; bu faz avukatla
     başlar, migration'la değil.

**Ayrıca yapıldı (2026-09-07) — dönem penceresi:**

Metrikler tüm zamanların toplamıydı; hiçbir yerde tarih filtresi yoktu. Bu
sadece sunum eksiği değil, **adalet sorunuydu**: iki yıl önce herkesi yok sayıp
sonra düzelmiş bir şirket eski sayısını sonsuza kadar taşır, düzelmesinin
sayıya yansımasının yolu olmaz. Artık `CompanyIntelligence:WindowMonths`
(varsayılan 12) ile pencereleniyor, `AppliedAt` üzerinden — böylece örneklem
temiz bir kohort oluyor ("bu dönemde yapılan başvurular"), eski bir başvuruyla
yeni bir durum değişikliği karışmıyor. Yanıt artık `WindowStart`/`WindowEnd`
taşıyor; `Hidden` durumunda bile, çünkü "bu dönemde eşiğin altında" ile "hiç
bu kadar olmadı" farklı iddialar ve yalnızca ilki doğru.

**Yayından önce kapatılması gereken açık madde:** çok yeni başvurular
yanıtlanmaya vakit bulamadığı için yanıt oranını aşağı, ghosting oranını yukarı
çekiyor. 12 ayda sapma küçük, 1 ayda olmazdı. Dürüst çözüm, ghosting eşiğinden
genç başvuruları bu iki paydadan çıkarmak — ama bu sayıların *anlamını*
değiştirir, dolayısıyla adalet/hukuk incelemesinin parçası, tek başına bir kod
değişikliği değil. Bkz. `CompanyIntelligenceOptions.WindowMonths` yorumu.
- **Bu yüzden en sonda.** Kod hazır olması onu ilk sıraya taşımıyor; K1'i
  bugün açmak boş ekran yayınlamak demek.

### Sıra 6 (paralel, küçük) — K6: Geri bildirimin GitHub Issues aynası ✅ (2026-09-07)

- **Ne vardı:** `Feedback:GitHub:Enabled=false`. Geri bildirim DB'ye yazılıyor,
  GitHub'a aynalanmıyordu.
- **Kilidi (kapatıldı):** Gizlilik sayfası metni güncellenmeden açılmayacağı 2026-09-07
  kararında yazılı (bkz. DECISIONS.md "Uygulama içi geri bildirim").
- **Not:** İç akış meselesi, kullanıcı özelliği değil. Sırayı bloklamadı,
  gizlilik metni güncellendiği gün açıldı.

> **Yapıldı (2026-09-07), özelliğin kendisiyle aynı gün — ön koşul önce kapatılarak.**
> Sıra kasıtlıydı: bayrak varsayılan kapalıyken `/privacy#feedback` "hiçbir üçüncü tarafa
> gönderilmez" diyordu ve bu cümle yalnızca bayrak kapalıyken doğruydu. Önce gizlilik metni
> yazıldı (`web/messages/{tr,en}.json` → `privacy.feedback.mirror` / `mirrorRecipient` /
> `deletion` ve `privacy.transfers.feedbackTransfer`; alıcı GitHub, Inc. (Microsoft, ABD)
> olarak ve bu bir yurt dışı aktarımı olarak adlandırılıyor, "yurt dışına aktarım" bölümü
> artık tek değil iki aktarımı anlatıyor), sonra bayrak açıldı.
>
> Prod'da açık: `.github/workflows/deploy.yml` → `Feedback__GitHub__Enabled=true`,
> `Repository=selcukgural/ekariyerim-feedback` (private, kod yok, sadece issue),
> `Assignee=selcukgural`; token Secret Manager'da (`afterapply-feedback-github-token`).
> `appsettings.json`'daki `Enabled=false` **lokal varsayılan ve öyle kalıyor** — geliştirme
> ortamı ve test host'ları kazara dışarı çıkmasın diye; ortam değişkeni onu prod'da eziyor.
> Bu yüzden repodaki `false` değerine bakıp "ayna kapalı" sonucuna varılmamalı.
>
> Aynanın redaksiyonu (mesaj + teknik bağlam gider; ad, hesap e-postası ve yanıt adresi
> gitmez — yalnızca feedback id gider) `GitHubIssueComposer`'da, bayrağın **iki yönlü** bağı
> — kapatılırsa gizlilik metninin de geri alınması gerekir — `FeedbackGitHubOptions`
> yorumunda yazılı.
>
> **Açık kalan, ama K6'ya ait olmayan madde:** kullanıcı kendi geri bildiriminin durumunu
> göremiyor. `Status` ve `AdminReply` kolonları ilk günden var, okuma ucu
> (`GET /api/feedback/mine`) çağıranı olmadığı için bilerek yazılmadı (2026-09-07 kararı).
> Bu bir gizli özellik değil, yazılmamış bir özellik — envantere girmez.

### Bu envanterde **olmayanlar** (yanlış hatırlanmasın diye)

- **Gmail taraması, red gerekçesi çıkarımı, HR kontağı yakalama** — üçü de
  canlı. Chrome eklentisi `0.6.0` Web Store'da yayında; `0.7.0` 2026-09-09'da
  yüklendi ve **incelemede** (bkz. V3).
  (Güncelleme 2026-09-09: `extension/store-listing/PUBLISHING_CHECKLIST.md` artık
  güncel — "hâlâ `0.4.0` yazıyor" uyarısı geçersiz, o dosya bugün yayının gerçek
  durumunu tutan tek kaynak.)
- **Gmail OAuth entegrasyonu** — gizli değil, 2026-08-31'de koddan tamamen
  silindi (CASA değerlendirmesinin maliyeti kabul edilmedi).

---

## Vaat, ölçüm ve erişim — V0-V5 (2026-09-08)

> **Bağlam:** K1-K6 envanteri "yazılmış ama kullanıcıya ulaşmayan"ı topluyordu.
> Bu bölüm bir sonraki soruyu ele alıyor: **kullanıcı neden hiç gelmiyor.**
> Çıkış noktası iki rakip iş modelinin (twiser.com, lattice.com) incelenmesi ve
> ardından yapılan kod denetimi. Gerekçenin tamamı ve kaynak alıntıları
> "Vaadi Değiştirmek" notunda:
> https://claude.ai/code/artifact/89ac552a-03eb-41f3-b601-9697bbdbf76b

**Sıralama ilkesi K1-K6'dan devralınıyor** — kilidi bizde olan iş önce — ve
üstüne bir kural ekleniyor: **ölçemeden değiştirme.** Numara = yapılma sırası,
V = kalıcı kimlik.

### Kabul edilen teşhis

Takip **ürün değil, veri toplama mekanizması.** Kimse "başvuru takip aracı"
istemiyor; iş istiyor. Bunu iki bağımsız kaynak destekliyor: (a) aynı pazardaki
Huntr ve Teal saf takipten CV/otomatik doldurmaya göç etti — bir pazarda herkes
aynı yöne gidiyorsa o göç bir bilgi taşır; (b) bizim `landing.hero.eyebrow`
değerimiz bugün hâlâ "İş başvuru takibi", yani angaryayı satıyoruz.

Değişmesi gereken kod değil, cümle: *"Başvurularını tek yerde takip et"* →
*"Başvuruların nereye gidiyor?"*. Yeni vaat bugün zaten dürüst — `AnalyticsService`
yanıt oranını, ghosting oranını ve medyan yanıt süresini hesaplıyor.

**Cold-start düzeltmesi:** Lattice'in yıllık sektör raporu ürün telemetrisi değil,
**1.002 kişilik bir anket** (2 Nisan - 5 Haziran 2025). Yani toplu şeffaflık verisi
kullanıcı beklemek zorunda değil — sorarak da üretilebilir. Sıra
"kullanıcı → veri" değil, **"sayı → kullanıcı"**.

### Sıra 1 — V0: Birinci taraf, çerezsiz ölçüm ✅ (2026-09-08)

- **Ne yok:** `web/package.json` içinde Sentry dışında hiçbir analitik yok. K5
  kayıtlı kullanıcıyı ölçüyor (aktivasyon, WAU, D7/D30/D90) ama **ziyaretçi →
  kayıt hunisi tamamen görünmez.**
- **Neden üçüncü taraf araç takılamaz — bu madde sıradan bir iş değil:**
  Çerez Politikası yayında ve *"Google Analytics benzeri bir analitik aracı ya da
  herhangi bir üçüncü taraf çerezi kullanmayız"* diyor; devamında analitik
  eklenirse **önce sayfanın güncelleneceği ve varsayılanı kapalı bir onay
  ekranının devreye alınacağı** taahhüt ediliyor. Üstelik CSP yalnızca kendi
  origin'imize, kendi API'mize ve Sentry'ye izin veriyor. GA4/Plausible/PostHog =
  taahhüt ihlali + onay ekranı maliyeti + konumlandırma hasarı.
- **Yapılacak:** Kendi API'mize giden, kimlik saklamayan, çerezsiz olay sayacı —
  yol, dil, referans eden alan adı, gün. Kişi tanımlayıcısı ve IP saklanmaz.
  Çerez yazmadığı ve kimlik tutmadığı için onay ekranı gerekmez, ama şeffaflık
  için Çerez + Gizlilik sayfalarına bölüm eklenir.
- **Ölçülecek asıl şey:** `landing → kayıt → ilk başvuru`. İkinci yarıyı K5
  zaten biliyor.
- **Kilidi:** Bizde.
- **Neden ilk:** V1-V5'in hiçbirinin sonucu bu olmadan okunamaz.

> **Yapıldı (2026-09-08).** `SiteTrafficDailyCounters` tablosu (gün, olay, sayfa, dil, referans
> eden host → sayı); `POST /api/site-traffic/events` anonim + IP bazlı hız sınırlı (120/5dk);
> `GET /api/admin/site-traffic` ve `/admin/metrics` sayfasında özet + en çok görüntülenen sayfalar
> + nereden gelindiği; `(public)` layout'una takılan `SiteTrafficReporter`; Çerez ve Gizlilik
> sayfalarına "Ziyaret sayacı" bölümü (tr+en, ikisinin de tarihi 8 Eylül'e çekildi).
>
> Dört tasarım kararı kasıtlı:
>
> 1. **Ziyaretçi kimliği yok — hiç.** Çerez, oturum, IP, user-agent saklanmıyor; aynı kişinin iki
>    ziyareti ile iki kişinin birer ziyareti aynı sayı. Bunun bedeli huninin **iki bağımsız toplamın
>    oranı** olması ("40 ana sayfa, 1 kayıt"), izlenen bir yolculuk olmaması. Bu trafikte sorulan
>    tek soruyu ("gelen var mı, devam eden var mı") cevaplamaya yetiyor ve sormadan toplanabilecek
>    olanın dürüst sınırı.
> 2. **Yol allowlist'ten geçiyor, temizlenmiyor.** `SiteTrafficNormalizer` query string'i her şeyden
>    önce kesiyor (reset token'ı, OAuth code'u, arama terimi orada), sonra sabit bir genel sayfa
>    listesine bakıyor. Giriş yapılmış hiçbir sayfa — dolayısıyla hiçbir kayıt id'si — tabloya
>    ulaşamıyor, ve satır sayısı sayfa sayısıyla sınırlı kalıyor. OAuth callback yolları listede
>    **yok ve olmayacak.**
> 3. **Referrer host'a indirgeniyor**, tarayıcıdan çıkmadan önce (`buildSiteTrafficPayload`) ve bir
>    kez daha sunucuda. Arama terimi hiç yola çıkmıyor.
> 4. **Yazma tek bir `ON CONFLICT` upsert'ü**, oku-değiştir-yaz değil: aynı saniyede aynı sayfaya
>    giren herkes aynı satırda çakışıyor — bu tablonun istisnası değil normali.
>
> **Neden üçüncü taraf araç takılmadı:** Çerez Politikası yayında ve *"Google Analytics benzeri bir
> analitik aracı ... kullanmayız"* diyor; devamında analitik eklenirse önce sayfanın güncelleneceği
> ve **varsayılanı kapalı bir onay ekranı** konulacağı taahhüt ediliyor. O taahhüt isteğe bağlı
> **çerezler** için; çerez yazmayan, kimlik tutmayan birinci taraf sayaç onu tetiklemiyor — ama yine
> de iki sayfaya da anlatıldı. CSP zaten yalnızca kendi origin + kendi API + Sentry'ye izin veriyor.
>
> **Testler (unit 453'e, web 214'e, integration 256'ya çıktı).** İki tanesi drift bekçisi:
> `routes.test.ts` C# allowlist'ini kaynaktan okuyup **her genel sayfanın sayılabildiğini ve hiçbir
> korumalı sayfanın sayılamadığını** doğruluyor (sayfa eklenip listeye yazılmazsa sessizce
> ölçülmeden kalırdı); `browserStorage.test.ts` — zaten çerez politikasının bekçisi — artık
> `trackSiteTraffic`'i çağırabilecek dosyaları da sabitliyor ve sayacın `apiFetch` kullanmadığını
> (kullansa ziyaretçinin token'ını iliştirirdi) kontrol ediyor.
>
> **Bulunan gerçek:** SQL enjeksiyonu sorusu üzerine yazılan test bir varsayımı düzeltti —
> `evil.example/'); DROP TABLE...` referrer'ında host geçerli olduğu için **saklanıyor**; atılan şey
> payload'ı taşıyan *yol*. Test artık bunu ölçüyor.

### Sıra 2 — V1: Vaadi değiştir ✅ (2026-09-08)

- **Yapılacak:** `web/messages/{tr,en}.json` içinde `landing.hero.*`,
  `landing.problem.*`, `landing.afterApply.*`; meta/OG etiketleri; yardım
  metinleriyle tutarlılık.
- **Kilidi:** Bizde.
- **Kural:** V0'dan sonra. A/B değil — trafik bunu taşımaz — öncesi/sonrası ölçüm.

> **Yapıldı (2026-09-08), V0 ile aynı sürümde.** Deploy süresi nedeniyle ikisi birlikte çıkıyor;
> **bunun bedeli "öncesi" ölçümünün kaybı.** İlk trafik sayıları yalnızca yeni metnin sayılarıdır
> — eski metinle kıyaslanamaz, ve ileride öyle okunmamalı.
>
> Değişen 15 dize (tr+en): `landing.hero.eyebrow`/`subtitle`, `landing.problem.title`/`body2`/
> `outcome`, `landing.afterApply.note`, `landing.analytics.*`, `landing.finalCta.title`/`body`,
> `metadata.description` ve `metadata.pages.{home,register,login}.description`.
>
> - **`hero.title` değişmedi.** "Başvurdun. Peki sonra ne oldu?" zaten yeni vaadin ta kendisiydi;
>   angaryayı satan eyebrow ("İş başvuru takibi") ve alt başlıktı ("tek yerde takip et").
> - **Yeni alt başlık pasif toplamayı öne çıkarıyor** ("içe aktar ya da eklentiyi kur — toplamayı
>   e-kariyerim yapsın"), çünkü Excel'e karşı gerçek fark arayüz değil, yazmak zorunda olmamak.
> - **Problem bölümü artık parçalanmayı adlandırıyor:** "Başvuru geçmişin dört ayrı yere dağılmış.
>   Dördü de unutuyor." Bölümdeki dört kaynak görseli zaten oradaydı, metin onu söylemiyordu.
>   Gövde metni uydurma değil — kariyer.net'in eski başvuruları göstermemesi üzerine rehber
>   yazısı zaten var.
> - **`analytics.title` "piyasa nasıl" demiyor,** çünkü henüz cevaplayamıyoruz (o V2/K1). "Kaçından
>   cevap geldi? Kaçı hiç dönmedi?" bugün dürüst olan en güçlü hâli.
>
> **Bilgi mimarisi:** `AnalyticsSection` ve `LinkedInImportSection` özellik listesinin **üstüne**
> alındı. Sayfa hero → problem → neden → *özellik listesi* → içe aktarma → sayılar şeklindeydi;
> yani ürünün asıl olduğu şey altıncı bölümdeydi. Yeni sıra: neden'in hemen ardından sayılar, onun
> ardından "kendini nasıl dolduruyor", özellik listesi en sonda. `#how-it-works` (AfterApply) ve
> `#features` (Features) id'leri yerinde kaldı, navbar ve hero'nun ikincil CTA'sı etkilenmedi.
>
> **Sıralama bir kusur açtı ve kapatıldı:** `AnalyticsSection` beyaz ve `border-t`'siz idi, çünkü
> eskiden gri `LinkedInImportSection`'ı takip ediyordu; yeni yerinde beyaz `AfterApplySection`'ın
> altına düşünce iki bölüm ayırıcısız birleşiyordu. `border-t` eklendi. Tarayıcıda doğrulandı.
>
> **`<title>` bilerek değişmedi.** "İş Başvuru Takip Uygulaması" / "Job Application Tracker" bir
> gün önce SEO için özellikle konmuştu: başlık birinin arattığı şeyi karşılar, hero ise "neden
> umursayayım"ı. Kimse "başvurularımın kaçı cevaplandı" diye aramıyor, kategoriyi arıyor. Açıklama
> ikisinin buluştuğu yer — sonuçla açılıyor, terimi hâlâ içeriyor. `routes.test.ts` terimi
> sabitliyor ki ileriki bir metin turu sessizce silmesin.

### Sıra 3 — V2: Girişsiz kıyas aracı ("Geri dönüş oranın normal mi?") ✅ (2026-09-08)

- **Yapılacak:** `(public)` altında kayıt istemeyen sayfa. Kullanıcı üç dört sayı
  girer (başvuru, dönüş, sektör, opsiyonel şehir/kıdem), kendi oranını ve
  *n kişiden biri* olduğunu görür.
- **Dürüstlük kuralı K1'den devralınıyor:** örneklem eşiğin altındayken **kıyas
  gösterilmez**, yalnızca kendi sayın ve n. K1'deki merdivenin (50/100/250/1000)
  mantığı aynen geçerli.
- **Sektör ekseni serbest metin değil**, K4'ün yüzeye çıkardığı `Industry`
  alanıyla hizalı sabit liste — yoksa veri kıyaslanamaz hale gelir.
- **Güvenlik:** Yeni tablo kimliksiz (kayıtlı kullanıcıda `UserId` opsiyonel).
  Hız sınırı + bot koruması zorunlu; PII toplanmaz, serbest metin alınmaz.
- **Kilidi:** Bizde.
- **Bu madde aynı zamanda tezin testi** — bkz. "Durdurma koşulu".

> **Yapıldı (2026-09-08).** `/tr/benchmark` ve `/en/benchmark`: dört zorunlu soru (başvuru sayısı,
> dönüş sayısı, dönem, alan) + iki isteğe bağlı (kıdem, konum), kayıt istemeden.
> `BenchmarkSubmissions` tablosu, `POST /api/benchmark/submissions` ve `GET /api/benchmark/summary`
> — ikisi de anonim, IP bazlı saatlik 5 istek sınırı. Footer'a ve landing'in Analytics bölümüne
> link kondu (öncesinde sayfaya hiçbir yerden ulaşılamıyordu).
>
> **Kıyasın üç kapsamı var** (`BenchmarkComparisonScope`), ve aradaki fark sayfanın dürüstlüğünün
> tamamı: `Sector` (alan eşiği geçti), `Overall` (alan geçmedi ama genel havuz geçti — medyan
> **açıkça "tüm alanlar geneli" diye etiketlenir**, alan iddiası olarak sunulmaz), `None` (ikisi de
> yetersiz). Genel havuz fallback'i ekip kararıyla eklendi: sektör iddiasının barı yine 30'da
> kaldı, ama sayfanın ilk haftalardaki tek cevabı "henüz değil" olmaktan çıktı.
>
> Beş karar kasıtlı:
>
> 1. **`UserId` yok** — plan "kayıtlı kullanıcıda opsiyonel" diyordu. Kullanıcıya bağlı tablo olurdu
>    (cascade kuralı) ve kimsenin istemediği bir özellik için beyan verisine kimlik iliştirirdi.
>    Bedeli — aynı kişi iki kez cevaplayabilir — metodoloji notunda yazılı; savunması **ortalama
>    yerine medyan**, ki birkaç uç/tekrar cevabı neredeyse hiç oynatmıyor (unit test bunu ölçüyor).
> 2. **Sektör ekseni sabit bir enum, `Company.Industry` değil.** O kolon LinkedIn'den kazınan
>    serbest metin (200 karakter, nullable, kontrolsüz). Ürün verisiyle havuzlanacaksa gereken şey
>    o metinden bu enum'a bir eşleme; yok, ve anket tek kaynak olduğu sürece gerekmiyor.
> 3. **Karşılaştırma hücresi yalnızca sektör, dönem değil.** Oran bir orandır, dönemler arası
>    havuzlanmaya sayıdan çok daha dayanıklı; iki eksene birden bölmek her hücreyi uzun süre eşiğin
>    altında bırakırdı. Dönem yine saklanıyor ve uyarısı metodoloji notunda: kısa dönem düşük
>    görünür, çünkü yakın başvurular cevaplanmaya vakit bulamamıştır.
> 4. **Rota yerelleştirilmedi** (`/benchmark` iki dilde de). next-intl'e `pathnames` eklemek tek bir
>    sayfa için tüm uygulamanın `Link` href tiplerini değiştirirdi; arama terimleri title,
>    description ve H1'de taşınıyor.
> 5. **CAPTCHA yok, olamaz.** reCAPTCHA/hCaptcha/Turnstile üçü de üçüncü taraf script — CSP
>    yasaklıyor, Çerez Politikası "yok" diyor. Yerine honeypot alanı + hız sınırı + sınır
>    kontrolleri. V0'ı şekillendiren kısıtın aynısı.
>
> **Testler:** unit 462 → 465, web 216 → 225, integration 256 → 271. Yeni drift bekçisi
> `options.test.ts`: form seçenekleri C# enum'larıyla **iki yönlü** eşleşmeli ve her seçeneğin iki
> dilde etiketi olmalı — biri eksikse kullanıcı 400 alır ya da ham anahtar görür, ve ikisi de o
> seçenek seçilene kadar görünmez.
>
> **Tarayıcıda üç kapsam da doğrulandı** ve iki hata yalnızca bakınca çıktı: (a) `%60'i` Türkçede
> yanlış — kesme eki sayının okunuşuna uyar (altmış→`'ı`, sıfır→`'ı`, otuz→`'u`), değişken bir sayı
> için doğrusunu üretmek sayı-okuma mantığı ister; cümle eki gerektirmeyen kalıba çevrildi.
> (b) Büyük oranın altındaki "27 / 90 başvuru" satırı yazılmıştı ama yanıt o iki sayıyı
> taşımıyordu; sonuca eklendi.
>
> **V0'ın drift bekçisi bir gün sonra işini gördü:** yeni sayfa `SiteTrafficNormalizer`'ın
> allowlist'inde olmadığı için `routes.test.ts` düştü — ziyaretleri hiç sayılmayacaktı.

### Sıra 4 — V3: Eklenti eşleştirmesi — altı adım, bir tık olmalı ✅ (2026-09-08)

- **Bugünkü durum (denetimde çıktı):** Eklentinin çalışması için kullanıcı
  kayıt ol → Ayarlar → anahtar üret → kopyala → eklenti seçenekleri → yapıştır
  adımlarını geçmek zorunda. Anahtar bir kez gösteriliyor ve yapıştırma anında
  ekranda *"Bu anahtar hesabınıza tam erişim sağlar"* yazıyor.
- **Üstelik sessizce ölüyor:** `PersonalAccessTokens.LifetimeDays = 90`. 90 gün
  sonra eklenti çalışmayı bırakıyor, kullanıcı hiçbir şey yapmamış olsa bile.
- **Yani "eklenti-önce huni" bir sıralama değişikliği değil.** Ürünün en düşük
  efor eşiğine sahip parçasının önünde ürünün en yüksek eşiği duruyor.
- **Yapılacak:** Web'den başlatılan eşleştirme (tek kullanımlık kısa kod ya da
  `externally_connectable`), süre dolmadan uyarı, dolduğunda popup'ta net hata.
- **Kilidi:** Bizde, ama eklenti sürümü + Web Store yeniden yüklemesi gerektiriyor
  (CLAUDE.md "Chrome extension release policy": manifest sürümü, izin gerekçesi,
  gizlilik metni, ekran görüntüleri, zip).
- **Değişmez kısıt:** `/from-extension` yalnızca eklemeye açık — yayındaki her
  eklenti sürümü çalışmaya devam etmeli (DECISIONS.md 2026-09-06).

> **Yapıldı (2026-09-08), eklenti `0.7.0` olarak.** Seçilen yol **kısa kod / cihaz akışı**:
> `ExtensionPairingRequests` tablosu, `POST /api/extension-pairing/requests` + `/poll` (anonim, IP
> bazlı 10/5dk ve 600/5dk) ve girişli `requests/{code}/approve|deny`; `/{tr,en}/pair` onay sayfası;
> eklentide Bağlan düğmesi, kod gösterimi ve poll döngüsü.
>
> **Neden `externally_connectable` değil:** o yalnızca "önce kaydolmuş" kişiyi kurtarıyordu. Cihaz
> akışı Store'dan gelen hesapsız kişiyi de kapsıyor — onay sayfası girişsiz açılıyor, kayıttan sonra
> kullanıcıyı koduyla birlikte geri getiriyor (`?next=` yalnızca `/pair` şeklini kabul eden bir izin
> listesinden geçiyor). Yeni izin gerekmedi.
>
> Üç karar kasıtlı:
>
> 1. **Token onayda değil, toplamada üretiliyor.** Approve yalnızca kimin onayladığını yazar; PAT
>    ilk `poll`'da mint edilir ve satır aynı işlemde tüketilir. Böylece tabloda hiçbir zaman ham bir
>    sır durmaz ve kimsenin toplamadığı onay arkasında kimlik bilgisi bırakmaz.
> 2. **Kod bir kimlik, sır değil.** Onay girişli oturum ister, toplama ise yalnızca eklentinin
>    tuttuğu 256 bitlik cihaz sırrıyla yapılır. Cihaz akışının tek gerçek saldırısı — "kodu sana biri
>    gönderirse" — metinle karşılanıyor: kod büyük gösteriliyor, ne verildiği yazılıyor ve
>    **"Bu ben değilim"** bir düğme.
> 3. **Eklenti token'ı kendi halefini onaylayamaz** (`approve` `.AllowExtensionToken()` almıyor →
>    403), yoksa sızmış bir anahtar kendini süresiz yenilerdi.
>
> **90 gün kaldı, sessizliği kalktı:** eşleştirme son kullanma tarihini de veriyor, eklenti onu
> saklıyor, son 14 günde uyarıyor, dolduğunda "yeniden bağlan" diyor ve 401'i ağ hatasından ayırıyor
> — eskiden ikisi de "e-kariyerim'e ulaşılamadı" idi.
>
> **Yan bulgu:** Ayarlar'daki *"Bu anahtar hesabınıza tam erişim sağlar"* cümlesi 2026-09-03'ten beri
> yanlıştı (kapsam o gün `Extension`'a inmişti); web ve yardım metinleri düzeltildi. Ayrıca ekran
> görüntüsü çekimi bir hata yakaladı: `popup.css`'in `button { display: block }` kuralı UA
> stilindeki `[hidden]`'ı yeniyordu, `el.hidden` bir düğmede işe yaramıyordu.
>
> Testler: unit 479, web 232, integration 287. Eklenti tarafında harness yok; akış yerel yığında
> tarayıcıda uçtan uca doğrulandı.
>
> **Kullanıcıya ulaşma durumu (2026-09-09): henüz ulaşmadı.** `0.7.0` Dashboard'a yüklendi ve
> **incelemede**; onaylanana kadar herkes `0.6.0` çalıştırıyor, yani Bağlan akışı kodda var ama
> kimsenin tarayıcısında yok. V3'ün huni etkisi ancak inceleme geçtikten sonra ölçülebilir —
> ilk trafik sayıları hâlâ altı adımlı eşleştirmenin sayılarıdır.

### Sıra 5 — V4: Haftalık ritim (K3'ün üstüne)

- **Ne var:** K3 durum geçmişi + elle eklenen olayları tek "Süreç" listesinde
  birleştirdi. Ama **pasif** — başvuru detayının içinde duruyor, oraya gitmeyi
  seçen görüyor.
- **Yapılacak:** Çıkarım katmanı — bu hafta ne kıpırdadı, ne sessizleşti, neye
  takip zamanı geldi. **Kişinin kendi ortalamasına göre**, toplu veri beklemeden:
  "senin ilk dönüşün normalde 9 gün, bu 21 gündür sessiz" n=1'de çalışır.
- **Yüzey:** Önce panoda bir blok. E-posta özeti ayrı bir altyapı ve izin konusu,
  sonraya.
- **Kilidi:** Kodu bizde ama tutundurma işi — gelen kimse yokken ölçülemez.
  Bu yüzden V0-V3'ten sonra.

### Sıra 6 (paralel, kod değil) — V5: Dağıtım

- Kitlenin zaten olduğu yer: Ekşi, r/CodingTR, TR yazılım toplulukları, LinkedIn.
  Ayrıca **hâlâ kullanılmayan bir kanal:** Chrome Web Store'un kendi araması.
- **Buraya ürün linkiyle gidilmez, sayıyla gidilir.** Paylaşılan şey link değil
  rakam olmalı.
- **Kilidi:** V2'nin ürettiği ilk sayı. Sayı yoksa paylaşılacak şey de yok.

### Durdurma koşulu (tezin çürütülmesi)

V2 yayınlanır ve V5'teki kanallara sayıyla gidilir. **Birkaç yüz kişi kendi
sayısını girmiyorsa ve kimse paylaşmıyorsa tez yanlıştır** — insanlar gerçekten
sadece işi umursuyordur, şeffaflık bizim ilgimizdi. Maliyeti bir sprint değil
birkaç gün, ve çıktısı "haklıydım/haksızdım" değil rakam.

Aynı test **K1'in kaderini de belirliyor:** kimse sektör sayısını umursamıyorsa
şirket bazlı sayıyı hiç umursamaz, ve o hukuki görüş masrafına hiç girilmemiş
olur. K1'in kalan iki kilidi (veri hacmi, hukuk) zaten bizde değil.
