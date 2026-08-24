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

## MVP sonrası (spec §22 Phase 9-13) — sıralama önerisi

Spec'teki sıralamayı koruyoruz, ancak Phase 10 (Company Intelligence) için
**veri hacmi gate'i** var (§17, §15): yeterli anonim veri birikmeden bu
fazın açılmaması gerekir.

1. Phase 9 — Email Integration (Gmail/Outlook, read-only, kullanıcı onaylı)
2. Phase 10 — Company Intelligence (**gate:** aggregate edilebilir minimum
   veri hacmi + confidence threshold'ları netleşmeden başlamaz)
3. Phase 11 — AI (CV/job matching, follow-up generation)
4. Phase 12 — Browser Extension
5. Phase 13 — B2B (employer dashboard, candidate experience score)

Bu sıralama, kullanıcı değeri (retention'ı artıran Email Integration) ile
network value (veri hacmi gerektiren Company Intelligence) arasındaki
bağımlılığı önceliklendiriyor: önce retention/veri girişini artır, sonra
üzerine intelligence katmanını kur.
