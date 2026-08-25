# AfterApply — Development Plan

Kaynak: `afterapply-intelligence-platform-plan.md` (product/technical spec).
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
- Docker Compose dev environment (API + PostgreSQL + Redis)
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

Phase 9 (Email Integration — sadece Gmail) zaten tamamlandı (bkz.
DECISIONS.md). Kalan fazlar şu sırayla planlanıyor:

---

## Sprint 8 — AI Job Matching (spec Phase 11)

> **Durum (2026-08-25): backend + frontend implementasyonu tamamlandı**
> (unit testler yeşil — bkz. DECISIONS.md "Sprint 8 kararları ve bulguları").
> Bekleyen: gerçek `OpenAI:ApiKey` ile manuel smoke test, ve podman
> entegrasyon testlerinin (`MatchingTests.cs`) çalıştırılması (workflow
> kararı gereği bu oturumda koşulmadı).

> Diğer data-gated fazlardan farklı olarak bu faz **tek kullanıcının kendi**
> CV'si + job description'ına dayanıyor, başka kullanıcı verisine bağımlı
> değil — dolayısıyla yayın öncesi tam olarak bitirilebilir.

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
> gerçek bug bu doğrulama sırasında bulunup düzeltildi). Bekleyen: podman
> entegrasyon testlerinin (`PersonalAccessTokenTests.cs`,
> `ExtensionApplicationTests.cs`) koşulması (workflow kararı gereği bu
> oturumda koşulmadı) ve Chrome Web Store yayını (kapsam dışı, plan
> zaten böyle diyordu).

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
> yeşil; podman entegrasyon testi `CompanyIntelligenceTests.cs` yazıldı,
> Sprint 8/9'un bekleyen suite'iyle birlikte batch sonunda koşulacak —
> workflow kararı gereği bu oturumda koşulmadı). UI yok (plan zaten böyle
> diyordu — Sprint 10 sadece altyapı).

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

- Composite score: Responsiveness / Response Time / Closure Rate /
  Interview Experience / Process Transparency alt metrikleri, Sprint 10'un
  aggregate'lerinden türetilir
- Ağırlıklandırma formülü — spec somut bir formül vermiyor, sprint
  başında netleştirilecek (OPEN)
- Aynı `CompanyIntelligence:Enabled` flag'i altında (ayrı bir flag
  gereksiz — aynı aktivasyon koşuluna, gerçek veri hacmine bağlı)

**DoD:** Sprint 10'daki gibi sentetik veriyle doğrulanan, flag kapalıyken
gizli bir skor hesaplama pipeline'ı.

---

## Sprint 12 — B2B Employer Dashboard iskeleti (spec Phase 13)

> En düşük öncelik. Bu faz gerçek, ödeme yapan işveren müşterisine muhtaç
> — bu bir mühendislik teslimi değil, sales/go-to-market fonksiyonu.
> Şirket doğrulama/sahiplik modeli iş modeli kararı gerektirdiği için bu
> sprint **kullanıcı onayı olmadan başlatılmamalı**.

- Employer hesap tipi + şirket sahiplik/doğrulama akışı — OPEN
- Şirketlerin kendi Candidate Experience verisini görebileceği salt-okunur
  dashboard (Sprint 10/11 aggregate'lerini reuse eder)
- Kapsamı ve zamanlaması Sprint 8-11 tamamlandıktan sonra yeniden
  değerlendirilecek

---

## Sprint 13 — Launch Hazırlığı v2

- Cloud provider kararı finalize (DECISIONS.md §5, Sprint 7'den beri
  OPEN — artık gerçek launch yaklaştığı için ertelenemez)
- Prod secrets/observability (error tracking servisi — OPEN)
- Son privacy/legal review (ToS, KVKK/GDPR self-review — hukuki onay
  gerektirir, bu doküman hukuki tavsiye değildir)
- Domain/branding finalize
- Sprint 8-12'nin tüm entegrasyon testleri + uçtan uca manuel smoke test

**DoD:** Prod-benzeri ortamda spec §4.4 MVP kriteri + Sprint 8-12'nin
DoD'leri uçtan uca doğrulanabilir durumda; yayına hazır.
