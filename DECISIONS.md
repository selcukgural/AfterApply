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

## 3. Frontend framework — OPEN (Sprint 2'ye kadar ertelendi)

Backend First ilkesi (§2) gereği bu karar Sprint 2 başına kadar
ertelenebilir. Öneri hâlâ Next.js/React; Blazor alternatif olarak
değerlendirilebilir (tek dil, daha az context-switch). Sprint 2 başında
netleştirilecek.

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

## Spec dokümanındaki küçük tutarsızlıklar (bilgi amaçlı, aksiyon gerektirmiyor)

- Bölüm numaralandırması §32'den sonra §35, sonra §34, sonra §36 şeklinde
  karışık — muhtemelen yazım sırasında sıralama değişmiş.
- §30 sıralaması GDPR'ı KVKK'dan önce listeliyor; Türkiye-first
  pozisyonlamayla tutarlı olması için KVKK önce değerlendirilebilir (hukuki
  görüş gerektirir, bu doküman hukuki tavsiye değildir).
