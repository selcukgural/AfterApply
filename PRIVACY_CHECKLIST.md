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

## Henüz uygulanamaz (özellik yok) — N/A

| Madde | Neden |
|---|---|
| Email permission boundaries | Email entegrasyonu (Phase 9) MVP kapsamında değil; e-posta adresi sadece login-identifier olarak kullanılıyor, başka hiçbir amaçla işlenmiyor. |
| Public analytics anonimleştirme / minimum örneklem | Public/şirket-görünür analytics (Phase 10+) MVP kapsamında değil; `IAnalyticsService` bugün sadece kullanıcının kendi verisini kendisine döndürüyor, hiçbir agregasyon başkasına gösterilmiyor. |

## Kabul edilen risk / backlog

| Madde | Not |
|---|---|
| Token storage (`localStorage`) | Access/refresh token'lar httpOnly cookie yerine `localStorage`'da tutuluyor (`web/src/lib/api/tokenStorage.ts`) — önceki sprintlerden gelen mimari tercih. Değiştirmek `AuthContext`/`authStore`/`tokenStorage`/her `apiFetch` çağrısını etkileyen büyük bir refactor; bu sprintin kapsamında istenmedi, backlog'a not düşüldü. |
| Consent versiyonlama / re-consent | `ConsentAcceptedAt` sadece kayıt anında bir kere set ediliyor; privacy policy içerik olarak değişirse mevcut kullanıcılardan yeniden onay istenmesi gereken bir akış yok. Backlog. |
| Hangfire dashboard auth | `/hangfire` bu sprintte de eklenmedi (Sprint 6'da ertelendi) — hâlâ backlog. |
