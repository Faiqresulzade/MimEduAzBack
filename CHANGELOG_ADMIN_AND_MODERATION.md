# Dəyişiklik siyahısı — Təlim CRUD, moderasiya, aldıqlarım, e-poçt bildirişi, admin icmalı

Bu sənəd 6 tələbin backend həllini əhatə edir. Bloq CRUD artıq mövcud idi,
dəyişiklik tələb olunmadı (bax. § 6).

---

## 1. Təlim CRUD (redaktə və silmə)

**Problem:** yalnız `POST /trainings` var idi. Admin təlimi nə redaktə, nə də
silə bilirdi.

**Dəyişiklik:**
- `PUT /trainings/{id}` (Admin) — ad, format, təsvir, qiymət, müddət, meta
  etiket və (opsional) proqram maddələrini yeniləyir. Dərslər bu endpoint-ə
  daxil deyil, artıq mövcud olan `POST /trainings/{id}/lessons` ilə idarə
  olunur.
- `DELETE /trainings/{id}` (Admin) — təlimi silir. **Qeydiyyatı olan təlim
  silinmir, `409 Conflict` qaytarılır** (istifadəçilərin aldığı məzmun və
  sertifikat tarixçəsi kaskad silinməsin deyə). Silinərkən təlim kiminsə açıq
  səbətindəsə həmin sətir də təmizlənir — əks halda checkout zamanı 404 verərdi.
- Yer limiti mövcud qeydiyyat sayından aşağı endirilə bilmir (`409`/`400`).

**Fayllar:** `TrainingService.cs` (`UpdateAsync`, `DeleteAsync`),
`ITrainingService.cs`, `TrainingsController.cs`, `TrainingDtos.cs`
(`UpdateTrainingRequest`), `CatalogValidators.cs`.

---

## 2. Moderator resurs detallarını görür

**Problem:** `ResourceMappings.ToDetailDto` ödənişli video/link resurslarında
`externalUrl`-i yalnız pulsuz olduqda dolduranurdu. Admin moderasiya
zamanı materialı görmədən təsdiq/rədd qərarı verməli olurdu.

**Dəyişiklik:** `ResourceViewContext` (IsPurchased / IsAuthor / IsAdmin)
əlavə edildi — link həmişə **müəllifə, adminə və satın alana** göstərilir,
kənar istifadəçidən gizli qalır. `GET /admin/resources/pending` artıq
`ResourceDto[]` yox, **`ResourceDetailDto[]`** qaytarır (link, fayl adı,
rədd səbəbi daxil). `Approve`/`Reject` cavabları da eyni qaydadan keçir.

**Fayllar:** `ResourceMappings.cs`, `AdminService.cs`, `IAdminService.cs`,
`AdminController.cs`.

---

## 3. "Aldığım resurslar" (satın alınmış görünüş)

**Problem:** istifadəçi ödənişli resursu alsa da, kataloqda "Səbətə at"
düyməsi görünməyə davam edirdi — frontend-in nə alındığını ayırd etməsi üçün
sahə yox idi. Ayrıca "aldıqlarım" siyahısı ümumiyyətlə yox idi.

**Dəyişiklik:**
- `ResourceDto`/`ResourceDetailDto`-ya **`isPurchased`** və **`canAccess`**
  sahələri əlavə edildi. `canAccess` — pulsuzdursa, satın alınıbsa, müəllifdirsə
  və ya admindirsə `true`.
- **`GET /resources/purchased`** (Authorize) — cari istifadəçinin ödənişi
  tamamlanmış sifarişlərindəki resursları qaytarır.
- Siyahı/detal/müəllif profili cavablarının hamısı tək sorğu ilə (N+1 yox)
  satın alınma vəziyyətini hesablayır.

**Fayllar:** `ResourceDtos.cs`, `ResourceMappings.cs`, `ResourceService.cs`
(`GetPurchasedAsync`, `BuildViewAsync`, `MapManyAsync`), `IResourceService.cs`,
`ResourcesController.cs`.

---

## 4. Moderasiya sorğusu üçün e-poçt bildirişi

**Problem:** resurs moderasiyaya düşəndə admin bunu yalnız panelə girərək
öyrənirdi.

**Dəyişiklik:** `RequestLogQueue`/`RequestLogWriter` ilə eyni nümunə —
**Channel əsaslı növbə + arxa plan servisi**:
- `IEmailQueue` / `EmailQueue` (`System.Threading.Channels`, tutum 200,
  dolarsa ən yeni mesaj atılır — bildiriş heç vaxt sorğunu ləngitmir).
- `IEmailSender` / `SmtpEmailSender` (MailKit 4.17.0 — GHSA-9j88-vvj5-vhgr
  aradan qaldırılıb). SMTP konfiqurasiya olunmayıbsa (`Email:Host` boşdursa)
  **məktub göndərilmir, yalnız loga yazılır** — sistem SMTP-siz də işləməlidir.
- `EmailBackgroundSender` (`BackgroundService`) növbəni boşaldır.
- `INotificationService.NotifyAdminsOfPendingResourceAsync` — alıcılar
  **bazadan bütün Admin rollu istifadəçilər** (+ opsional `Email:ModerationInbox`).
  Bütün metod try/catch ilə əhatə olunub: bildiriş uğursuz olsa belə resursun
  yüklənməsi pozulmur.
- `ResourceService.CreateAsync`/`CreateLinkAsync` hər ikisi yeni resurs
  moderasiyaya düşəndə bildirişi tetikləyir.

**Konfiqurasiya (`appsettings.json` → `Email`):**
```json
"Email": {
  "Enabled": true,
  "Host": "", "Port": 587, "UseSsl": false,
  "Username": "", "Password": "",
  "FromAddress": "", "FromName": "MIMEDU.AZ",
  "ModerationInbox": "",
  "SiteUrl": "https://mimedu.az",
  "TimeoutSeconds": 15
}
```
`Host`/`FromAddress` boş qaldıqca `IsConfigured = false` və göndərmə sükutla
keçilir. Prodda Render-də bu dəyərləri env dəyişənləri ilə doldurun
(`Email__Host`, `Email__Username`, və s.).

**Fayllar:** `EmailOptions.cs`, `IEmailSender.cs`, `INotificationService.cs`,
`NotificationService.cs`, `Infrastructure/Email/EmailQueue.cs`,
`Infrastructure/Email/SmtpEmailSender.cs`, hər iki `DependencyInjection.cs`.

---

## 5. Admin icmalı (dashboard)

**Yeni endpoint:** `GET /admin/dashboard` (Admin).

```
{
  "traffic": { totalRequests, requestsLast24Hours, failedRequests,
               clientErrors, serverErrors, serverErrorsLast24Hours,
               averageDurationMs, errorRatePercent },
  "content": { totalUsers, totalResources, pendingResources, approvedResources,
               rejectedResources, totalDownloads, blogPosts, issuedCertificates },
  "trainings": { totalTrainings, liveTrainings, onlineTrainings, videoTrainings,
                 totalLessons, totalEnrollments, completedEnrollments,
                 soldTrainings, trainingRevenue },
  "sales": { gmv, commissionTotal, authorPayoutTotal, paidOrders, pendingOrders,
             soldResources, resourceRevenue },
  "topErrors": [ { method, path, statusCode, count, lastOccurredAt } ],
  "topTrainings": [ { trainingId, name, enrollmentCount, revenue } ],
  "generatedAt": "..."
}
```

Bütün saylar audit log (`request_logs`) və satış (`order_items`) cədvəllərindən
tək keçidli qruplaşdırma sorğuları ilə hesablanır (panel açılanda 10 ayrı
sorğu getməsin deyə). `topErrors` üçün EF-in `GroupBy → record proyeksiyası`nı
tərcümə edə bilmədiyi üçün əvvəlcə anonim tipə yığılır, DTO yaddaşda qurulur.

**Fayllar:** `AdminDtos.cs` (yeni DTO-lar), `AdminService.cs`
(`GetDashboardAsync` + 6 köməkçi metod), `IAdminService.cs`,
`AdminController.cs`.

---

## 6. Bloq CRUD

Artıq tam mövcuddur (`GET/GET-by-id` açıq, `POST/PUT/DELETE` yalnız Admin) —
`BlogController.cs`. Heç bir dəyişiklik tələb olunmadı.

---

## 7. Baza dəyişikliyi

**Yoxdur.** Bu bloklardakı bütün dəyişikliklər mövcud cədvəllər üzərində
işləyir (yeni sütun/cədvəl əlavə olunmayıb). `dotnet ef database update`
lokal və Supabase-də "The database is already up to date" nəticəsini verir.

---

## 8. Yoxlama nəticələri

| Test | Nəticə |
|---|---|
| Unit testlər (8 yeni `TrainingAdminTests`) | **68/68** |
| `scripts/smoke-test.mjs` | **100/100** |
| `scripts/student-flow-test.mjs` | **41/41** |
| `scripts/link-and-certificate-test.mjs` (yenilənmiş moderator assersiyası) | **33/33** |
| `scripts/admin-and-purchases-test.mjs` (yeni, 64 assertion) | **64/64** |

Bütün skriptlər **təzə (sıfırdan seed edilmiş) verilənlər bazası** ilə ayrı-ayrı
işə salınıb — eyni bazada ardıcıl skript qaçırmaq seed vəziyyətini dəyişdiyi
üçün "uğursuzluqlar" real reqressiya deyil, test-sırası çarpazlaşmasıdır (əvvəlki
sessiyalarda da müşahidə olunub).

---

## 9. Frontend üçün qeydlər

- `ResourceDto`/`ResourceDetailDto`-da **`isPurchased`/`canAccess`** yeni
  sahələrdir — kataloq kartında "Səbətə at" düyməsini `canAccess`-ə görə
  gizlədin, "Aldıqlarım" səhifəsini `GET /resources/purchased` ilə qurun.
- `GET /admin/resources/pending` artıq `ResourceDetailDto[]` qaytarır
  (əvvəlki `ResourceDto[]`-dan fərqli sahələr var: `externalUrl`,
  `originalFileName`, `rejectionReason`, `quizId`).
- Admin panelinin baş səhifəsi `GET /admin/dashboard`-dan tək sorğu ilə
  qurulmalıdır.
- Təlim redaktə forması `PUT /trainings/{id}` çağırmalı, silmə düyməsi
  `409` cavabında "bu təlimə qeydiyyat var, silinə bilməz" mesajını
  göstərməlidir.
