# Job Application Intelligence Platform

## 1. Ürün Vizyonu

Bu proje, iş arayan insanların yaptıkları iş başvurularını kolayca takip
etmelerini, başvuru süreçlerinin durumunu görmelerini ve zaman
içerisinde oluşan anonim veriler üzerinden şirketlerin işe alım
süreçleri hakkında anlamlı istatistikler üretmelerini sağlayan bir
platformdur.

Temel problem:

> İş arayan insanlar onlarca/yüzlerce başvuru yapıyor; hangi şirkete ne
> zaman başvurduklarını, hangi aşamada olduklarını, kimlerin cevap
> verdiğini, ne kadar sürede cevap verdiğini ve hangi şirketlerin hiç
> cevap vermediğini takip etmekte zorlanıyor.

Daha büyük problem ise adayların işe alım süreçleri hakkında yeterli
kolektif veriye sahip olmamasıdır.

Uzun vadeli hedef:

**Personal Job Search Operating System + Candidate Experience
Intelligence Platform**

Kullanıcı tarafı:

Find Job → Evaluate → Apply → Track → Interview → Follow-up → Outcome →
Learn

Platform tarafı:

Collect → Normalize → Aggregate → Analyze → Benchmark → Publish Insights

Şirket tarafı:

Measure → Benchmark → Identify Problems → Improve Candidate Experience

------------------------------------------------------------------------

## 2. Ürün Prensipleri

### Privacy First

Kullanıcının bireysel başvuru verileri varsayılan olarak private
olmalıdır. Public analytics yalnızca anonim ve aggregate veriden
üretilmelidir.

### Statistical Integrity

Küçük örneklem üzerinden şirketler hakkında kesin hüküm verilmemelidir.
Public company analytics sample size ve confidence seviyesi
göstermelidir.

### Candidate First

İlk kullanıcı iş arayan kişidir. Employer analytics daha sonraki
aşamadır.

### Automation First

Kullanıcı mümkün olduğunca az manuel veri girmelidir. Uzun vadede
LinkedIn import, CSV import, Gmail/Outlook, browser extension ve
otomatik status detection desteklenmelidir.

### Backend First

İlk MVP gereksiz UI karmaşıklığından kaçınmalıdır. Ana değer veri
modeli, application lifecycle, analytics ve automation tarafındadır.

------------------------------------------------------------------------

# 3. Pazar ve Hedef Kullanıcı

## 3.1 Pazar Stratejisi: Türkiye-First

Ürün **Türkiye-first** olarak geliştirilecektir.

İlk hedef:

> Türkiye'deki iş arayan beyaz yaka ve özellikle teknoloji profesyonellerinin iş başvurusu süreçlerini daha düzenli, ölçülebilir ve şeffaf hale getirmek.

Türkiye-first olmak ürünün domain modelinin Türkiye'ye kilitlenmesi anlamına gelmez.

İlk günden global düşünülmesi gereken alanlar:

- Country
- Currency
- Language
- Timezone
- EmploymentType
- RemoteType
- Company
- Job
- Application

Ancak ilk MVP:

- Türkçe kullanıcı deneyimi
- Türkiye'deki şirketler
- Türkiye'deki iş ilanları
- Türkiye'deki adayların problemleri
- Türkiye'deki işe alım davranışları

üzerine odaklanacaktır.

İleride genişleme:

```text
Türkiye
   ↓
Avrupa
   ↓
Global
```

## 3.2 İlk Hedef Kullanıcı

İlk kullanıcı segmenti:

- Türkiye'de iş arayan beyaz yaka profesyoneller
- özellikle teknoloji çalışanları
- Senior / Lead / Staff seviyesindeki profesyoneller
- çok sayıda başvuru yapan kişiler
- aktif iş arama sürecinde olan kişiler

İlk wedge özellikle **teknoloji profesyonelleridir**.

Bunun nedenleri:

- LinkedIn kullanımının yüksek olması
- çok sayıda online başvuru yapılması
- teknik rollerin yüksek başvuru hacmi
- problemin sık yaşanması
- kurucunun bu kullanıcı grubunu doğrudan tanıması
- ilk kullanıcıların kişisel network üzerinden daha kolay bulunabilmesi

Daha sonra:

```text
Technology
→ Finance
→ Sales
→ Marketing
→ Operations
→ White Collar
→ All Job Seekers
```

şeklinde genişlenebilir.

## 3.3 Ürün Konumlandırması

İlk ürün mesajı:

> **Yaptığın tüm iş başvurularını tek yerde takip et. Hangi şirketin ne zaman cevap verdiğini gör. Cevap vermeyenleri unutma.**

Uzun vadeli mesaj:

> **Türkiye'deki işe alım süreçlerini daha şeffaf hale getiren veri platformu.**

# 4. MVP — İlk Kullanıcıya Verilecek Değer

MVP'nin tek amacı:

> **Kullanıcının yaptığı iş başvurularını tek yerde tutmasını, durumlarını kolayca güncellemesini ve kendi iş arama sürecini ölçebilmesini sağlamak.**

İlk sürüm:

**Job Application Tracker + Personal Analytics**

olacaktır.

İlk sürüm bir Company Intelligence ürünü, AI Job Search Assistant veya B2B employer platformu değildir.

## 4.1 MVP Kullanıcı Akışı

```text
Register
   ↓
Add / Import Application
   ↓
See Application List
   ↓
Update Status
   ↓
See Timeline
   ↓
See Personal Analytics
   ↓
Receive Follow-up / No-response Reminder
```

## 4.2 MVP'de Kesinlikle Olacaklar

### User Account

- kayıt
- login
- logout
- profil
- account deletion
- personal data export

### Application Management

Kullanıcı:

- başvuru ekleyebilir
- başvuruyu düzenleyebilir
- başvuruyu silebilir
- status değiştirebilir
- not ekleyebilir
- başvuru URL'sini açabilir
- filtreleyebilir
- arayabilir
- sıralayabilir

Temel model:

```text
Application
-----------
Id
UserId
CompanyId
JobId
JobTitle
JobUrl
Location
EmploymentType
AppliedAt
Status
Source
Notes
CreatedAt
UpdatedAt
```

### Standard Application Lifecycle

```text
Applied
Screening
Interview
TechnicalInterview
FinalInterview
Offer
Accepted
Rejected
Withdrawn
Ghosted
```

`Draft` ilk MVP'de zorunlu değildir.

### Application Timeline

Her application için:

- oluşturulma
- status değişimleri
- kullanıcı notları
- önemli tarihler

timeline olarak gösterilmelidir.

```text
ApplicationEvent
----------------
Id
ApplicationId
Type
OccurredAt
Source
Metadata
CreatedAt
```

Örnek event tipleri:

- ApplicationCreated
- ApplicationSubmitted
- RecruiterContacted
- ScreeningStarted
- InterviewScheduled
- InterviewCompleted
- OfferReceived
- Rejected
- Withdrawn
- FollowUpSent
- StatusChanged

### Dashboard

```text
Total Applications
Active Applications
Waiting
Interviews
Offers
Rejected
Ghosted
```

### Personal Analytics

İlk sürüm:

- response rate
- interview rate
- offer rate
- rejection rate
- ghosting rate
- average response time
- median response time

### Reminder

Basit reminder sistemi bulunmalıdır.

Örnek:

> "ABC şirketine başvurunun üzerinden 8 gün geçti. Takip etmek ister misin?"

Ghosting için sistem önce:

> "Possibly Ghosted"

şeklinde öneri üretmelidir.

### CSV Import

Generic CSV import desteklenmelidir.

### LinkedIn Data Export Import

LinkedIn export MVP'nin önemli differentiator'ıdır.

```text
Upload ZIP
→ Find Job Applications CSV files
→ Parse
→ Normalize
→ Deduplicate
→ Import
→ Summary
```

Örnek:

```text
Total records: 1,136
New applications: 1,020
Duplicates: 116
Invalid records: 0
```

Import idempotent olmalıdır.

## 4.3 MVP'de Olmayacaklar

Aşağıdakiler bilinçli olarak MVP dışındadır:

- AI Job Matching
- Gmail / Outlook Integration
- Browser Extension
- Public Company Intelligence
- Candidate Experience Score
- Employer Dashboard
- Social Network
- Automated Application Submission
- Complex AI Automation
- Kullanıcı adına otomatik başvuru yapma

İlk MVP'de LLM zorunlu dependency olmamalıdır.

## 4.4 MVP Başarı Kriteri

Bir kullanıcı:

```text
LinkedIn export'unu yükler
→ yüzlerce başvurusunu içeri alır
→ duplicate'ler temizlenir
→ dashboard'da başvurularını görür
→ status'lerini düzenler
→ timeline'larını görür
→ response/interview/offer oranlarını görür
→ cevap bekleyen başvurularını bulur
```

ve bunu spreadsheet kullanmadan yapabiliyorsa MVP başarılıdır.

## 4.5 MVP'nin "Wow Moment"i

İlk önemli ürün deneyimi:

> Kullanıcı LinkedIn başvuru geçmişini yüklediğinde yüzlerce başvurunun birkaç dakika içinde düzenli ve anlamlı bir dashboard'a dönüşmesi.

Bu nedenle LinkedIn import yalnızca teknik bir özellik değil, ilk ürün farklılaştırıcısıdır.

# 5. Ghosting

Kullanıcı Ghosted statusunu manuel verebilir.

İleride sistem otomatik öneri üretmelidir:

``` text
if application has no response
and last activity > configurable threshold
then suggest "Possibly Ghosted"
```

Başlangıçta 30 gün kullanılabilir ancak hard-code edilmemelidir.

``` text
GhostingThresholdDays = 30
```

İleride şirketin historical response distribution'ı dikkate alınabilir.

------------------------------------------------------------------------

# 6. Company Model

``` text
Company
-------
Id
Name
NormalizedName
Website
LinkedInUrl
Industry
Country
CreatedAt
UpdatedAt
```

Company identity normalize edilmelidir.

Örneğin:

``` text
ABC Teknoloji
ABC Technology
ABC Tech
```

aynı şirket olarak resolve edilebilmelidir.

İlk MVP'de basit normalization yeterlidir; ileri aşamada fuzzy matching
/ external identity resolution eklenebilir.

------------------------------------------------------------------------

# 7. Job Model

``` text
Job
---
Id
CompanyId
Title
NormalizedTitle
Description
Url
Source
ExternalId
Location
RemoteType
EmploymentType
PublishedAt
ClosedAt
CreatedAt
UpdatedAt
```

Özellikle `Source + ExternalId` önemlidir.

Örneğin:

``` text
Source = LinkedIn
ExternalId = 4449445627
```

aynı ilana daha önce başvurulup başvurulmadığını tespit etmekte
kullanılabilir.

------------------------------------------------------------------------

# 8. Import System

MVP sonrasında ilk önemli özelliklerden biri import sistemidir.

Destek:

1.  CSV
2.  LinkedIn Data Export
3.  Browser Extension
4.  Gmail
5.  Outlook

## LinkedIn Import

LinkedIn export içerisindeki:

``` text
Jobs/Job Applications.csv
Jobs/Job Applications_1.csv
...
```

dosyaları parse edilebilmelidir.

Pipeline:

``` text
Upload ZIP
→ Extract
→ Find Job Applications files
→ Parse CSV
→ Normalize columns
→ Resolve Company
→ Resolve Job
→ Deduplicate
→ Create Applications
→ Import Summary
```

Örnek:

``` text
Total records: 1,136
New applications: 1,020
Duplicates: 116
Invalid records: 0
```

Import idempotent olmalıdır.

## Duplicate Detection

Öncelik:

1.  Source + ExternalJobId
2.  Job URL
3.  Company + JobTitle + AppliedAt
4.  Fuzzy matching

------------------------------------------------------------------------

# 9. Personal Analytics

Kullanıcı kendi verisini analiz edebilmelidir.

## Response Rate

``` text
Applications: 100
Responses: 63
Response Rate: 63%
```

## Response Time

Hem average hem median gösterilmelidir.

``` text
Average: 7.4 days
Median: 4 days
```

Median outlier etkisini azaltır.

## Interview Rate

``` text
Applications: 100
Interviews: 12
Interview Rate: 12%
```

## Offer Rate

``` text
Applications: 100
Offers: 2
Offer Rate: 2%
```

------------------------------------------------------------------------

# 10. Email Integration

MVP sonrasında Gmail ve Outlook bağlanabilir.

Amaç işe alımla ilgili e-mailleri tespit ederek application
timeline/status bilgisini otomatik güncellemektir.

Örnek:

``` text
"We'd like to invite you to an interview"
→ Interview

"Unfortunately, we have decided..."
→ Rejected

"We will get back to you..."
→ Waiting
```

Pipeline:

``` text
Email
→ Candidate matching
→ Application matching
→ Classification
→ Confidence score
→ Status suggestion
→ User confirmation
```

İlk versiyonda otomatik status değişimi yerine kullanıcı onayı tercih
edilmelidir.

Email access read-only olmalıdır. Email gönderme ayrı permission
gerektirmelidir.

------------------------------------------------------------------------

# 11. Browser Extension

Chrome/Edge extension ile LinkedIn job sayfasından:

-   company
-   title
-   URL
-   LinkedIn job ID
-   location
-   description
-   published date

alınabilmelidir.

Kullanıcı:

**I Applied**

dediğinde application oluşturulmalıdır.

------------------------------------------------------------------------

# 12. AI Job Matching

> **Ürün kapsamından tamamen kaldırıldı (2026-09-02).** Bu bölümde tarif edilen özellik
> (Sprint 8'de inşa edilmiş, sonra kullanıcı kararıyla koddan kaldırılmıştır) artık ürün
> planında yok — bkz. `DECISIONS.md`'nin ilgili girdisi ve `DEVELOPMENT_PLAN.md`'nin Sprint 8
> bölümü. Aşağıki metin sadece tarihsel referans olarak korunuyor.

Kullanıcının CV/profile bilgileri ile job description karşılaştırılır.

Örnek:

``` text
Application Score: 91/100

Strong Match:
+ C#
+ .NET
+ Microservices
+ Redis
+ RabbitMQ
+ Docker
+ Team Leadership

Missing:
- React
- Azure Functions

Recommendation:
APPLY
```

Bu özellik MVP'den sonra geliştirilmelidir. İlk hedef data collection ve
application tracking'dir.

------------------------------------------------------------------------

# 13. Company Intelligence

Yeterli anonim veri oluştuğunda şirket bazlı istatistikler:

``` text
Applications
Response Rate
Ghosting Rate
Average Response Time
Median Response Time
Interview Rate
Offer Rate
```

Örnek:

``` text
Company: XYZ

Applications: 8,421
Response Rate: 52%
Ghosting Rate: 48%
Average Response Time: 6.2 days
Median Response Time: 4 days
Interview Rate: 12%
Offer Rate: 2.8%
```

------------------------------------------------------------------------

# 14. Candidate Experience Score

İleride composite score:

``` text
Candidate Experience Score: 82/100
```

Alt metrikler:

-   Responsiveness
-   Response Time
-   Closure Rate
-   Interview Experience
-   Process Transparency

İlk MVP'de score algoritması yapılmamalıdır. Önce ham veri
toplanmalıdır.

------------------------------------------------------------------------

# 15. Statistical Confidence

Public company analytics için zorunludur.

Örnek:

``` text
Ghosting Rate: 62%
Applications: 7
Confidence: Very Low
```

vs.

``` text
Ghosting Rate: 48%
Applications: 8,421
Confidence: High
```

Başlangıç hipotezi:

``` text
< 20       Hidden
20-49      Very Low
50-199     Low
200-999    Medium
1000+      High
```

Bu eşikler ileride gerçek data ile değiştirilebilir.

------------------------------------------------------------------------

# 16. Public Analytics ve Fairness

Şirketler "iyi/kötü" diye etiketlenmemelidir.

Kötü:

> XYZ is a terrible company.

İyi:

> Based on 8,421 anonymized applications, 48% received no recorded
> response.

Dil veri odaklı ve tarafsız olmalıdır.

Yeterli sample size olmadan public company analytics gösterilmemelidir.

------------------------------------------------------------------------

# 17. Data Flywheel ve Uzun Vadeli Moat

Temel stratejik hipotez:

> **Tracker is the product entry point; application outcome data is the long-term moat.**

Kullanıcı kendi problemini çözmek için veri girer. Platform zaman içerisinde anonim ve aggregate olarak:

- application count
- response
- response time
- interview
- offer
- rejection
- no response
- application duration

gibi outcome verileri biriktirir.

```text
More Users
   ↓
More Applications
   ↓
More Verified Outcomes
   ↓
Better Company Intelligence
   ↓
More Useful Platform
   ↓
More Users
   ↺
```

MVP'de public analytics açılmayacaktır.

İlk aşama:

```text
Collect
→ Normalize
→ Validate
→ Aggregate
```

Yeterli veri oluştuğunda:

```text
Aggregate
→ Benchmark
→ Company Intelligence
→ Candidate Experience Insights
```

katmanı açılacaktır.

# 18. Monetization

## Free

-   sınırlı application
-   basic dashboard
-   basic analytics

## Pro

Başlangıç fiyat hipotezi: \$5-\$10/month.

Özellikler:

-   unlimited applications
-   LinkedIn import
-   email integration
-   automatic status detection
-   AI matching
-   follow-up assistant
-   advanced analytics
-   company intelligence

İlk MVP ücretsiz olabilir. Önce product-market fit doğrulanmalıdır.

## B2B

Uzun vadede:

**Candidate Experience Analytics**

şirketlere satılabilir.

Örnek dashboard:

``` text
Response Rate       82%
Average Response    4.3 days
Ghosting Rate        8%
Candidate Score      87/100
Industry Average    71/100
```

------------------------------------------------------------------------

# 19. MVP Teknik Mimari

Önerilen stack:

## Backend

-   C#
-   .NET 10
-   ASP.NET Core
-   PostgreSQL
-   Redis

## Architecture

**Modular Monolith + Clean Architecture**

Mikroservis ile başlanmamalıdır.

Önerilen solution:

``` text
src/
  JobTracker.Api
  JobTracker.Application
  JobTracker.Domain
  JobTracker.Infrastructure
```

Modüller:

``` text
Identity
Applications
Companies
Jobs
Imports
Analytics
Notifications
```

İlk sürüm tek deployable application olabilir.

## Background Jobs

İlk aşamada Hangfire veya native hosted services değerlendirilebilir.

## API

REST API + OpenAPI/Swagger.

## Frontend

React/Next.js gibi modern frontend kullanılabilir ancak frontend
geliştirme ürünün backend/domain geliştirmesini yavaşlatmamalıdır.

## Deployment

Docker.

İlk cloud provider Azure veya AWS olabilir. Gereksiz abstraction
yapılmamalıdır.

------------------------------------------------------------------------

# 20. Database

PostgreSQL ana datastore.

Temel tablolar:

``` text
Users
Companies
Jobs
Applications
ApplicationEvents
ApplicationStatusHistory
ImportJobs
ImportRecords
EmailMessages
AnalyticsSnapshots
```

Kişisel veri ve audit ihtiyaçları dikkatle tasarlanmalıdır.

------------------------------------------------------------------------

# 21. API Taslağı

## Applications

``` http
GET    /api/applications
GET    /api/applications/{id}
POST   /api/applications
PUT    /api/applications/{id}
DELETE /api/applications/{id}

POST   /api/applications/{id}/status
GET    /api/applications/{id}/timeline
POST   /api/applications/{id}/events
```

## Companies

``` http
GET /api/companies
GET /api/companies/{id}
GET /api/companies/{id}/analytics
```

## Jobs

``` http
GET /api/jobs/{id}
POST /api/jobs
```

## Imports

``` http
POST /api/imports/linkedin
GET  /api/imports/{id}
```

## Analytics

``` http
GET /api/analytics/overview
GET /api/analytics/response-times
GET /api/analytics/status-distribution
```

------------------------------------------------------------------------

# 22. Development Phases

## Phase 1 --- Foundation

-   solution structure
-   Clean Architecture
-   dependency rules
-   Docker development environment
-   PostgreSQL
-   configuration
-   health checks
-   logging
-   test projects
-   README

## Phase 2 --- Identity

-   registration
-   login
-   token/session handling
-   user profile
-   privacy settings

## Phase 3 --- Application Domain

-   Company
-   Job
-   Application
-   ApplicationEvent
-   ApplicationStatusHistory
-   CRUD
-   status transitions
-   timeline

## Phase 4 --- UI

-   login
-   dashboard
-   application list
-   filters
-   application create/edit
-   application detail
-   timeline

## Phase 5 --- Analytics

-   response rate
-   interview rate
-   offer rate
-   rejection rate
-   ghosting rate
-   average response time
-   median response time

## Phase 6 --- CSV Import

-   CSV upload
-   parser
-   field mapping
-   validation
-   duplicate detection
-   import summary
-   idempotency

## Phase 7 --- LinkedIn Import

-   ZIP upload
-   ZIP extraction
-   Job Applications CSV discovery
-   parsing
-   LinkedIn Job ID extraction
-   company resolution
-   deduplication
-   import report

## Phase 8 --- Notifications

-   background jobs
-   reminders
-   follow-up reminders
-   no-response detection
-   configurable ghosting threshold

## Phase 9 --- Email Integration

-   Gmail OAuth
-   Outlook OAuth
-   synchronization
-   email classification
-   application matching
-   confidence score
-   user confirmation

## Phase 10 --- Company Intelligence

-   aggregation pipeline
-   sample size
-   response time statistics
-   ghosting statistics
-   interview/offer statistics
-   confidence calculation
-   public company analytics

## Phase 11 --- AI

-   CV/job matching
-   application score
-   follow-up generation
-   email classification improvements

## Phase 12 --- Browser Extension

-   LinkedIn extraction
-   Save Job
-   I Applied
-   API integration

## Phase 13 --- B2B

-   employer dashboard
-   candidate experience score
-   industry benchmark
-   company analytics

------------------------------------------------------------------------

# 23. First Sprint

İlk sprintte yalnızca foundation oluştur.

### Backend

-   solution
-   Clean Architecture
-   PostgreSQL
-   EF Core
-   configuration
-   health check
-   logging
-   tests
-   Docker
-   README

Henüz application feature'larını implement etme.

------------------------------------------------------------------------

# 24. Second Sprint

-   User
-   Company
-   Job
-   Application
-   ApplicationEvent
-   ApplicationStatusHistory
-   migrations
-   CRUD API
-   basic validation

------------------------------------------------------------------------

# 25. Third Sprint

-   dashboard
-   application list
-   filters
-   search
-   pagination
-   application detail
-   timeline
-   status management

------------------------------------------------------------------------

# 26. Fourth Sprint

-   response metrics
-   interview rate
-   offer rate
-   ghosting
-   average/median response time
-   charts

------------------------------------------------------------------------

# 27. Fifth Sprint

LinkedIn import:

``` text
Upload ZIP
→ Extract
→ Discover Job Applications files
→ Parse CSV
→ Normalize
→ Resolve Company
→ Resolve Job
→ Deduplicate
→ Import
→ Summary
```

Import idempotent olmalıdır.

------------------------------------------------------------------------

# 28. Product Metrics

Ölçülmesi gereken temel metrikler:

## Activation

Registration → First application

## Engagement

-   applications tracked/user
-   applications updated/user
-   weekly active users

## Retention

-   D7
-   D30
-   D90

## Data Network Effect

-   total applications
-   unique companies
-   unique jobs
-   applications with outcomes
-   applications with response time

------------------------------------------------------------------------

# 29. North Star Metric

İlk hipotez:

**Verified Application Outcomes**

Yani sonucu güvenilir biçimde bilinen başvuru sayısı.

Örneğin:

``` text
10,000 applications
6,200 verified outcomes
```

Çünkü uzun vadeli ürün değerini yalnızca application sayısı değil,
kaliteli outcome datası yaratır.

------------------------------------------------------------------------

# 30. Privacy / Legal / Ethical Requirements

Ürün kişisel iş arama verileri işleyecektir. Privacy-by-design
uygulanmalıdır.

Başlangıçtan itibaren:

-   privacy policy
-   explicit consent
-   account deletion
-   data deletion
-   data export
-   GDPR değerlendirmesi
-   KVKK değerlendirmesi
-   email permission boundaries
-   anonymization
-   minimum sample size
-   public analytics'te kişisel veri kullanmama

Şirket istatistikleri:

-   aggregate
-   anonymous
-   statistically meaningful
-   data-driven
-   neutral

olmalıdır.

------------------------------------------------------------------------

# 31. Claude Code Çalışma Kuralları

Claude Code bu dokümanı product + technical specification olarak kabul
etmelidir.

1.  MVP scope dışına çıkma.
2.  Gereksiz microservice oluşturma.
3.  Önce çalışan basit çözüm.
4.  Domain modelini analytics geleceğini düşünerek tasarla.
5.  Privacy-first yaklaşımı koru.
6.  Önemli her değişiklikte test yaz.
7.  API contractlarını açık tut.
8.  Database migrationları version-controlled tut.
9.  Configuration değerlerini hard-code etme.
10. External provider entegrasyonlarını abstraction arkasına al.
11. Idempotency gereken işlemleri idempotent tasarla.
12. Background job'ları retry-safe yap.
13. Kullanıcı verilerini loglara yazma.
14. Email içeriğini gereksiz yere persistent saklama.
15. Public analytics'e kişisel veri taşımama.
16. Küçük ve kontrollü değişiklikler yap.
17. Feature tamamlandığında test ve documentation güncelle.
18. Belirsiz bir product decision varsa varsayım yapmadan önce
    `DECISIONS.md` içine açık bir karar önerisi yaz ve kullanıcı onayına
    bırak.
19. Büyük refactor yapmadan önce mevcut davranışı koruyan test ekle.
20. YAGNI uygula.

------------------------------------------------------------------------

# 32. Claude Code'a Verilecek İlk Prompt

``` text
Bu dokümanı product specification ve technical direction olarak kabul et.

Öncelikle yalnızca PHASE 1'i implement et.

PHASE 1:
- solution structure
- Clean Architecture
- Modular Monolith foundation
- dependency rules
- Docker development environment
- PostgreSQL
- configuration
- health checks
- structured logging
- test projects
- README

Henüz Application, Company, Job veya kullanıcı özelliklerini implement etme.

Microservice oluşturma.

Kod üretmeden önce mevcut repository'yi incele.

Mevcut repository'de bir karar veya teknoloji seçimi varsa gereksiz yere değiştirme.

İş bittikten sonra:
1. oluşturduğun architecture'ı açıkla,
2. project dependency graphını ver,
3. çalıştırma komutlarını yaz,
4. eklediğin testleri listele,
5. varsa açık product/technical decision'ları DECISIONS.md'ye ekle.

Her aşamada gereksiz abstraction oluşturmaktan kaçın.
```

------------------------------------------------------------------------

# 35. Kararlaştırılmış ve Açık Ürün Kararları

## Kararlaştırıldı

### Market

**Türkiye-first, global-ready.**

### Initial User

Türkiye'deki beyaz yaka profesyonelleri; ilk wedge teknoloji profesyonelleri.

### MVP Positioning

**Job Application Tracker + Personal Analytics**

### Long-term Product

**Candidate Experience Intelligence Platform**

### Architecture

**Modular Monolith + Clean Architecture**

Microservice-first yaklaşım kullanılmayacaktır.

### Primary Data Source

Kullanıcının kendi application verisi.

LinkedIn Data Export import MVP'nin önemli differentiator'ıdır.

### Long-term Data Strategy

**Verified Application Outcomes** temel North Star veri varlığıdır.

## Hâlâ Açık

1. Authentication: ASP.NET Identity mi managed auth mu?
2. Frontend: React/Next.js veya alternatif?
3. Cloud: Azure mı AWS mi?
4. Background jobs: Hangfire mı native worker mı?
5. Exact free-tier limits
6. Pro pricing
7. Email integration provider priority
8. AI provider and cost strategy
9. Public company analytics minimum sample thresholds
10. Company identity resolution strategy
11. Browser extension release timing
12. Product name / brand

Bu kararların tamamının ilk sprintten önce verilmesi gerekmez.

# 34. En Önemli Stratejik Karar

Bu ürünün temel stratejisi:

> **Tracker is the product entry point; application outcome data is the
> long-term moat.**

Yani kullanıcı ilk olarak kendi başvurularını takip etmek için gelir.

Ancak her kaliteli ve anonim application outcome platformun uzun vadeli
veri değerini artırır.

Bu nedenle architecture iki ihtiyacı birlikte desteklemelidir:

``` text
User Value
Application Tracking
        +
Network Value
Anonymous Aggregate Hiring Intelligence
```

Birinci değer olmadan kullanıcı gelmez.

İkinci değer olmadan ürün uzun vadede sıradan bir job tracker olarak
kalabilir.

Hedef, ikisini aynı platformda birleştirmektir.


# 36. Current Product Scope Summary

```text
MARKET
Türkiye-first, global-ready

INITIAL USER
Türkiye'deki beyaz yaka
→ ilk wedge: teknoloji profesyonelleri

MVP
Job Application Tracker
+
Personal Analytics
+
CSV Import
+
LinkedIn Data Export Import
+
Reminders

NOT MVP
AI matching
Gmail/Outlook
Browser extension
Public company analytics
Candidate score
B2B dashboard

LONG TERM
Anonymous hiring intelligence
+
Candidate Experience Intelligence
+
Employer Analytics

ARCHITECTURE
Modular Monolith
+
Clean Architecture
+
PostgreSQL
+
Redis where justified

NORTH STAR DATA ASSET
Verified Application Outcomes
```

Bu scope değişmeden Claude Code'un MVP implementasyonuna başlaması önerilir.
