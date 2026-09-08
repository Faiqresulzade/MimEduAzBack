# MIMEDU.AZ — Frontend üçün API bələdçisi

Bu sənəd backend API-sinin frontend inteqrasiyası üçün lazım olan hər şeyi izah edir:
endpoint-lər, request/response formatları, auth axını və xəta idarəetməsi.

- **Base URL (dev):** `http://localhost:5297/api/v1`
- **Swagger UI:** `http://localhost:5297/swagger`
- **Format:** JSON (fayl yükləmə istisna — `multipart/form-data`)
- **Enum-lar** JSON-da **mətn** kimi gəlir (`"Approved"`, `"WorkSheet"`), rəqəm kimi yox.
- **Tarixlər** ISO 8601 UTC formatındadır (`"2026-03-14T12:00:00Z"`).
- **`null` sahələr** cavabda saxlanılır (məs. `seatLimit: null`) — `JSON.stringify` ilə silinmir.

> ⚠️ **Ödəniş demo rejimdədir.** Checkout heç bir kart məlumatı qəbul etmir.
> Kart forması tamamilə frontend-də qalır, backend-ə yalnız `{ customerName? }` göndərilir.

---

## 1. Autentifikasiya

### Token axını

1. `POST /auth/login` və ya `/auth/register` → `{ accessToken, refreshToken, accessTokenExpiresAt, user }`
2. Hər qorunan sorğuda header: `Authorization: Bearer <accessToken>`
3. Access token bitəndə (`401` alınanda) → `POST /auth/refresh` ilə yenisini al
4. Çıxışda → `POST /auth/logout` ilə refresh token-i ləğv et

**Saxlama tövsiyəsi:** `accessToken`-i yaddaşda (memory/state), `refreshToken`-i `httpOnly` cookie mümkün deyilsə `localStorage`-da saxlayın. Access token qısa ömürlüdür (dev: 60 dəq, prod: 15 dəq).

**Refresh rotasiyası:** hər `/auth/refresh` çağırışı **yeni** refresh token qaytarır və köhnəsini ləğv edir. Köhnə token-lə təkrar sorğu `401` verir — hər dəfə ən son gələni saxlayın.

### `POST /auth/register`
Qeydiyyat. `admin@mimedu.az` ilə qeydiyyatdan keçən avtomatik Admin rolu alır (seed davranışı).

**Hesab növü:** `accountType` göndərilməsə **`Student`** yaradılır — yəni default istifadəçi
təlim alan/keçən şagirddir. Material satmaq istəyən üçün `"Teacher"` göndərilir.

```ts
// Request
{
  fullName: string,
  email: string,
  password: string,
  subject?: string,
  accountType?: "Student" | "Teacher"   // default: "Student"
}

// 200 OK
{
  accessToken: string,
  refreshToken: string,
  accessTokenExpiresAt: string, // ISO date
  user: {
    id: string, fullName: string, email: string,
    subject: string | null,
    roles: string[],              // ["Student"] | ["Teacher"] | ["Admin"]
    canPublishResources: boolean, // Resurs yükləyə bilirmi (Teacher/Admin)
    createdAt: string
  }
}
```

Xətalar: `400` (zəif şifrə/format — `errors` obyektində sahə üzrə mesajlar), `409` (e-poçt artıq var).

### `POST /auth/become-author` 🔒
Şagird hesabını müəllif hesabına yüksəldir — bundan sonra Resurs Bankına material yükləyə bilər.
Artıq müəllifdirsə heç nə dəyişmir (idempotent).

```ts
// Request
{ subject?: string }   // müəllimin əsas fənni

// 200 OK — UserDto (roles: ["Student","Teacher"], canPublishResources: true)
```
⚠️ Yeni rol **mövcud access token-də əks olunmur** — çağırışdan sonra `POST /auth/refresh`
edin, əks halda `POST /resources` hələ də 403 verəcək.

### `POST /auth/login`
```ts
// Request
{ email: string, password: string }
// 200 OK → yuxarıdakı AuthResponse
```
Xəta: `401` (yanlış e-poçt/şifrə — hansının səhv olduğu bildirilmir).

### `POST /auth/refresh`
```ts
// Request
{ refreshToken: string }
// 200 OK → AuthResponse (yeni access + yeni refresh)
```
Xəta: `401` (token etibarsız/bitib/artıq istifadə olunub).

### `POST /auth/logout`
```ts
{ refreshToken: string }
// 204 No Content
```

### `GET /auth/me` 🔒
Cari istifadəçinin profili. `Authorization` header tələb olunur.
```ts
// 200 OK
{ id, fullName, email, subject, roles: string[], canPublishResources: boolean, createdAt }
```

---

## 2. Resurslar (Resurs Bankı)

### `GET /resources`
Publik. Default olaraq yalnız `Approved` statuslu resurslar qayıdır.

**Query parametrləri (hamısı opsional):**
| Parametr | Tip | Qeyd |
|---|---|---|
| `subject` | string | dəqiq uyğunluq, məs. `Riyaziyyat` |
| `grade` | int | 1–11 |
| `type` | `WorkSheet\|Presentation\|Test\|MethodGuide\|Video\|ExternalLink` | |
| `status` | `Pending\|Approved\|Rejected` | **yalnız Admin token-lə işləyir**, digərləri həmişə Approved görür |
| `isPaid` | bool | |
| `search` | string | ada görə axtarış, hərfə həssas deyil |
| `page` | int | default `1` |
| `pageSize` | int | default `20`, max `100` |

```ts
// 200 OK — PagedResult<ResourceDto>
{
  items: [{
    id, name, subject, grade,
    type: "WorkSheet",           // enum mətn kimi
    authorId, authorName,
    downloads: number,
    isPaid: boolean,
    price: number,               // AZN, pulsuzda 0
    status: "Approved",
    hasQuiz: boolean,
    isLinkBased: boolean,        // Video / ExternalLink — faylı yoxdur
    createdAt, approvedAt: string | null
  }],
  page: number, pageSize: number, totalCount: number, totalPages: number
}
```

### `GET /resources/{id}`
Publik (amma Pending/Rejected resurs yalnız müəllifi/admin görür — başqasına `404`).
```ts
// 200 OK — ResourceDetailDto
{
  id, name, subject, grade, type,
  authorId, authorName, authorSubject: string | null,
  downloads, isPaid, price,
  status: "Pending" | "Approved" | "Rejected",
  rejectionReason: string | null,   // yalnız Rejected-də dolu
  hasQuiz: boolean, quizId: string | null,
  originalFileName: string | null,
  isLinkBased: boolean,
  externalUrl: string | null,   // yalnız PULSUZ link resurslarında dolu
  createdAt, approvedAt
}
```

### `POST /resources` 🔒 (yalnız Teacher / Admin)
Yeni resurs yükləmə. **`multipart/form-data`** — JSON deyil!

⚠️ Şagird (`Student`) hesabı bu endpoint-ə **403** alır. Frontend "Material yüklə"
düyməsini `user.canPublishResources` sahəsinə görə göstərməlidir; şagirdə isə
`POST /auth/become-author` təklif olunmalıdır.

```
FormData:
  Name: string
  Subject: string
  Grade: number
  Type: "WorkSheet" | "Presentation" | "Test" | "MethodGuide"
  IsPaid: "true" | "false"
  Price: number          // IsPaid=false olarsa 0-a məcburi çevrilir
  file: File             // PDF/DOCX/PPTX, maks. 25 MB
```
⚠️ `Video` və `ExternalLink` bu endpoint-də **qəbul edilmir** (400) — onlar üçün
aşağıdakı `POST /resources/link` istifadə olunur.
```ts
// 201 Created — ResourceDetailDto (Status: "Pending")
```
Xəta: `400` (format/ölçü səhvdir — `errors.file` içində mesaj).

**React nümunəsi:**
```ts
const form = new FormData();
form.append('Name', name);
form.append('Subject', subject);
form.append('Grade', String(grade));
form.append('Type', type);
form.append('IsPaid', String(isPaid));
form.append('Price', String(price));
form.append('file', fileInput.files[0]);

await fetch(`${BASE}/resources`, {
  method: 'POST',
  headers: { Authorization: `Bearer ${accessToken}` }, // Content-Type QOYMAYIN, browser özü qoyur
  body: form,
});
```

### `POST /resources/link` 🔒 (yalnız Teacher / Admin)
Fayl yükləmədən **link əsaslı** resurs yaradır — JSON, multipart deyil.

| Tip | Nə üçün | Ödənişli ola bilər? |
|---|---|---|
| `Video` | YouTube/Vimeo video dərsi | ✅ Bəli |
| `ExternalLink` | Başqa saytda hazırlanmış material (Wordwall, Canva, Drive...) | ❌ Yalnız pulsuz |

```ts
// Request
{
  name: string,
  subject: string,
  grade: number,                       // 1–11
  type: "Video" | "ExternalLink",
  externalUrl: string,                 // tam URL (http:// və ya https://)
  isPaid?: boolean,                    // ExternalLink-də mütləq false
  price?: number
}
// 201 Created — ResourceDetailDto (status: "Pending")
```

Xətalar: `400` (səhv URL / ödənişli `ExternalLink` / fayl tipi göndərilib), `403` (şagird).

> **Niyə `ExternalLink` pulsuzdur:** link bir dəfə paylaşıldıqdan sonra ona nəzarət
> etmək mümkün deyil. Ödənişli satış üçün video dərs və ya fayl yükləmə istifadə olunur.

**Link necə açılır:**
- **Pulsuz** resursda `externalUrl` birbaşa `GET /resources/{id}` cavabındadır — video-nu
  dərhal embed edə bilərsiniz.
- **Ödənişli** videoda `externalUrl` **null** gəlir. Link yalnız satın alandan sonra
  `POST /resources/{id}/download` ilə alınır.

### `POST /resources/{id}/download`
Endirmə/açılış sayını artırır və linki qaytarır. Pulsuz resurs üçün token lazım deyil.
Ödənişli resurs üçün: token tələb olunur **və** həmin resurs əvvəlcə satın alınmalıdır (əks halda `403`).

```ts
// 200 OK
{
  resourceId, fileName,
  downloadUrl: string,      // fayl yolu VƏ YA xarici link
  isExternal: boolean,      // true → endirmə yox, yeni tabda aç
  downloads: number
}
```
`downloadUrl` API origin-ə nisbətdir — tam linki `${API_ORIGIN}${downloadUrl}` kimi qurun (`/api/v1` prefiksi olmadan, çünki statik fayllar `wwwroot`-dan verilir).

### `GET /resources/mine` 🔒
Cari istifadəçinin bütün resursları (bütün statuslar daxil) — `ResourceDto[]`.

### `GET /resources/author/{userId}`
Publik müəllif profili — yalnız təsdiqlənmiş resurslarla.
```ts
{ id, fullName, subject, resourceCount, totalDownloads, resources: ResourceDto[] }
```

### `GET /resources/{resourceId}/quiz`
Resursa bağlı imtahanın metadata-sı (suallar yoxdur).
```ts
{ id, resourceId, resourceName, passPercent: number, questionCount: number, createdAt }
```
`404` — resursda quiz yoxdursa.

### `POST /resources/{resourceId}/quiz` 🔒
Yalnız resursun müəllifi və ya Admin. Quiz yoxdursa yaradır, varsa sual əlavə edir/əvəz edir.
```ts
// Request
{
  passPercent: number,        // 1-100
  mode: "append" | "replace", // append: əlavə edir, replace: hamısını silib yenidən yazır
  questions: [{
    questionText: string,
    options: string[],        // ən azı 2
    correctOptionIndex: number
  }]
}
// 200 OK — QuizDto (yuxarıdakı kimi, suallar yenə də yoxdur)
```
Xəta: `403` (başqasının resursu).

---

## 3. Təlimlər

### `GET /trainings`
Publik. Bütün təlimlər.
```ts
// 200 OK — TrainingDto[]
[{
  id, name,
  format: "Live" | "Online" | "Video",
  description, price, durationHours,
  metaLabel: string,          // "2 gün · 8 saat"
  seatLimit: number | null,   // yalnız Live-da rəqəm, digərlərində null (limitsiz)
  seatsTaken: number,
  seatsLeft: number | null,   // seatLimit null-dursa bu da null
  lessonCount: number,        // təlimdə neçə dərs var
  createdAt
}]
```

### `GET /trainings/{id}`
Publik. `TrainingDetailDto` — yuxarıdakı + `syllabus: [{ id, orderIndex, text }]`,
`lessonCount: number` və `isEnrolled: boolean` (token göndərilibsə — "Dərslərə keç"
düyməsini göstərmək üçün).

### `POST /trainings` 🔒 (yalnız Admin)
```ts
{
  name, format: "Live"|"Online"|"Video", description,
  price: number, durationHours: number, metaLabel: string,
  seatLimit: number | null,   // Live üçün məcburidir
  syllabus: string[]
}
// 201 Created — TrainingDetailDto
```

### `GET /trainings/mine` 🔒
Cari istifadəçinin yazıldığı təlimlər (enrollment məlumatı ilə).
```ts
// MyTrainingDto[]
[{
  enrollmentId, trainingId, name, format, metaLabel, durationHours,
  progressPercent: number,
  completedLessonCount: number,
  totalLessonCount: number,
  status: "InProgress" | "Completed",
  enrolledAt,
  completedAt: string | null,
  certificateCode: string | null   // Completed olub sertifikat verilibsə dolu
}]
```

---

## 3a. Dərslər — istifadəçinin təlim keçmə axını 🔒

Bu, "istifadəçi girib təlimə baxır" hissəsidir. Ardıcıllıq:

```
GET  /trainings/{id}                          → isEnrolled: false, lessonCount: 4
POST /cart/items  { Training, id }            → səbətə at
POST /orders/checkout                         → enrollment yaranır
GET  /trainings/{id}/lessons                  → videolar açılır
POST /trainings/{id}/lessons/{lessonId}/complete   → hər dərsdən sonra
     ... sonuncu dərsdə → certificateCode qayıdır
```

### `GET /trainings/{trainingId}/lessons` 🔒
Təlimin dərsləri və irəliləyiş. **Yalnız bu təlimə yazılmış istifadəçi (və admin)** —
əks halda `403`. Token yoxdursa `401`.

```ts
// TrainingLessonsDto
{
  trainingId: string,
  trainingName: string,
  lessons: [{
    id: string,
    orderIndex: number,          // 0-dan başlayır, sıralı gəlir
    title: string,
    description: string,
    videoUrl: string | null,     // YouTube/Vimeo linki
    durationMinutes: number | null,
    isCompleted: boolean,
    completedAt: string | null
  }],
  completedLessonCount: number,
  totalLessonCount: number,
  progressPercent: number,
  status: "InProgress" | "Completed",
  certificateCode: string | null
}
```

### `POST /trainings/{trainingId}/lessons/{lessonId}/complete` 🔒
Dərsi tamamlanmış işarələyir. **Bütün dərslər bitəndə təlim avtomatik tamamlanır və
sertifikat verilir** — kodu cavabdakı `certificateCode`-da gəlir.

```ts
// LessonProgressDto
{
  trainingId: string,
  lessonId: string,
  isCompleted: boolean,
  completedLessonCount: number,
  totalLessonCount: number,
  progressPercent: number,        // avtomatik hesablanır
  status: "InProgress" | "Completed",
  certificateCode: string | null  // yalnız 100%-də dolu
}
```
Eyni dərsi təkrar göndərmək faizi artırmır (idempotent).

### `DELETE /trainings/{trainingId}/lessons/{lessonId}/complete` 🔒
"Tamamlandı" işarəsini geri götürür. Eyni `LessonProgressDto` qayıdır.

### `POST /trainings/{trainingId}/lessons` 🔒 (yalnız Admin)
Mövcud təlimə dərs əlavə edir və ya hamısını əvəz edir.
```ts
{
  mode: "append" | "replace",
  lessons: [{ title, description, videoUrl?, durationMinutes? }]
}
// 200 OK — TrainingLessonsDto
```
Dərs sayı dəyişəndə bütün yazılmış istifadəçilərin faizi avtomatik yenidən hesablanır.

---

## 4. Səbət və sifariş (demo checkout)

Bütün endpoint-lər 🔒 (Authorize).

### `GET /cart`
```ts
// CartDto
{
  id,
  items: [{ id, itemType: "Resource"|"Training", itemId, name, price, addedAt }],
  total: number
}
```

### `POST /cart/items`
```ts
// Request
{ itemType: "Resource" | "Training", itemId: string }
// 200 OK — CartDto (yenilənmiş)
```
Qaydalar (xəta halları):
- Pulsuz resurs səbətə əlavə oluna bilməz → `400`
- Öz resursunu almaq olmaz → `400`
- Təsdiqlənməmiş resurs → `400`
- Artıq səbətdədirsə → `409`
- Artıq satın alınıb / yazılıbsa → `409`
- Canlı təlimdə yer qalmayıbsa → `409`

### `DELETE /cart/items/{id}`
`{id}` — cart item id-si (resurs/təlim id-si deyil!). `200 OK` → yenilənmiş `CartDto`.

### `POST /orders/checkout`
Səbəti sifarişə çevirir, "Paid" statusunda tamamlayır, təlimlər üçün enrollment yaradır, səbəti boşaldır.
**Kart məlumatı göndərilmir!**
```ts
// Request (boş obyekt də olar)
{ customerName?: string }

// 201 Created — OrderDto
{
  id, code: "MIM-7113", customerName, totalAmount,
  status: "Paid",
  createdAt,
  items: [{ id, itemType, itemId, name, price }]
}
```
Xəta: `400` (səbət boşdur), `409` (təlimdə yer bitib).

### `GET /orders/mine`
Sifariş tarixçəsi — `OrderDto[]`.

---

## 5. İmtahan (Quiz)

### `GET /quiz/{quizId}/questions` 🔒
```ts
// QuizQuestionDto[] — correctOptionIndex HEÇ VAXT yoxdur!
[{ id, orderIndex, questionText, options: string[] }]
```

### `POST /quiz/{quizId}/submit` 🔒
```ts
// Request
{ answers: [{ questionId: string, selectedIndex: number }] }

// 200 OK — QuizResultDto
{
  attemptId, scorePercent: number, correctCount, totalQuestions,
  passPercent: number, passed: boolean,
  certificateCode: string | null   // passed=true olarsa avtomatik doludur
}
```
Keçid balı toplananda backend avtomatik sertifikat yaradır — ayrıca çağırış lazım deyil, cavabdakı `certificateCode`-u göstərin.

---

## 6. Sertifikatlar

### `GET /certificates/mine` 🔒
```ts
// CertificateDto[]
[{ id, code: "MIM-2026-4417", holderName, description, trainingId, trainingName, issuedAt }]
```

### `GET /certificates/verify/{code}`
**Publik** — token lazım deyil. Sertifikat doğrulama səhifəsi üçün.
Kod tapılmasa da `200` qaytarır, `isValid: false` ilə (404 yox!).
```ts
// 200 OK
{
  isValid: boolean,
  code: string,
  holderName: string | null,
  description: string | null,
  issuedAt: string | null
}
```

### `GET /certificates/{code}/download`
**Publik.** Sertifikatın A4 (landşaft) sənədini endirir — QR kod və MIMEDU.AZ imzası ilə.

| Query | Dəyər | Nəticə |
|---|---|---|
| `format` | `png` (default) | `image/png`, A4 landşaft, 150 DPI |
| `format` | `pdf` | `application/pdf`, çap üçün |

```
GET /api/v1/certificates/MIM-2026-4417/download          → PNG
GET /api/v1/certificates/MIM-2026-4417/download?format=pdf → PDF
```

Cavab fayl axınıdır (`Content-Disposition: attachment`), JSON deyil. Kod böyük/kiçik
hərfə həssas deyil. Mövcud olmayan kod → `404` (standart xəta formatında).

**Frontend nümunəsi:**
```ts
// Endirmə
window.location.href = `${BASE}/certificates/${code}/download?format=pdf`;

// Səhifədə göstərmək
<img src={`${BASE}/certificates/${code}/download`} alt="Sertifikat" />
```

Sertifikatdakı QR kod `Certificate:VerificationUrlTemplate` konfiqurasiyasındakı ünvana
aparır (default: `https://mimedu.az/sertifikat-yoxla/{code}`) — **bu marşrut frontend-də
mövcud olmalıdır**, əks halda QR skan edən 404 alacaq.

### `POST /certificates/issue` 🔒 (yalnız Admin)
```ts
{ userFullName: string, trainingId: string }
// 201 Created — CertificateDto
```
Xəta: `404` (istifadəçi tapılmadı), `409` (eyni adda bir neçə istifadəçi var).

---

## 7. Admin panel

Bütün endpoint-lər 🔒 yalnız Admin rolu.

| Endpoint | Cavab |
|---|---|
| `GET /admin/resources/pending` | `ResourceDto[]` — moderasiya növbəsi |
| `POST /admin/resources/{id}/approve` | `ResourceDetailDto` |
| `POST /admin/resources/{id}/reject` `{ reason?: string }` | `ResourceDetailDto` (`rejectionReason` dolu) |
| `GET /admin/users` | `AdminUserDto[]` (aşağıda) |
| `GET /admin/sales` | `SalesSummaryDto` (aşağıda) |
| `GET /admin/orders` | `OrderDto[]` — bütün sifarişlər |
| `GET /admin/logs` | `PagedResult<RequestLogDto>` — HTTP audit log (aşağıda) |

**Audit log** (`GET /admin/logs`) — bütün API sorğularının tarixçəsi.
Query: `method`, `path`, `statusCode`, `userId`, `onlyErrors`, `from`, `to`, `page`, `pageSize` (default 50, max 200).

```ts
// RequestLogDto
{
  id: number, traceId: string,
  method: string, path: string, queryString: string | null,
  statusCode: number, durationMs: number,
  userId: string | null, userEmail: string | null,
  ipAddress: string | null, userAgent: string | null,
  requestContentType: string | null,
  requestBody: string | null,    // şifrə/token sahələri "***" ilə maskalanıb
  responseBody: string | null,   // eyni maskalama tətbiq olunur
  exceptionType: string | null,
  createdAt: string
}
```

```ts
// AdminUserDto
{
  id, fullName, email, subject,
  roles: string[],
  resourceCount, approvedResourceCount, totalDownloads,
  enrollmentCount, certificateCount,
  createdAt
}

// SalesSummaryDto
{
  gmv: number,                 // ümumi dövriyyə
  commissionTotal: number,     // platformanın payı
  authorPayoutTotal: number,   // müəlliflərə ödənəcək
  orderCount: number,
  itemCount: number,
  resourceRevenue: number,
  trainingRevenue: number,
  commissionPercent: number    // 0.20 = 20%
}
```

---

## 8. Blog

### `GET /blog` — Publik
```ts
// BlogPostDto[]
[{ id, title, tag, readTime: "6 dəq", excerpt, createdAt }]
```

### `GET /blog/{id}` — Publik
`BlogPostDetailDto` — yuxarıdakı + `body: string[]` (paraqraflar).

### `POST /blog` 🔒 (Admin)
```ts
{ title, tag, readTime, excerpt, body: string[] }
// 201 Created — BlogPostDetailDto
```

### `PUT /blog/{id}` 🔒 (Admin)
Eyni body, `200 OK` — `BlogPostDetailDto`.

### `DELETE /blog/{id}` 🔒 (Admin)
`204 No Content`.

---

## 9. Xəta formatı (bütün endpoint-lər üçün ortaq)

```ts
// Uğursuz cavab body-si (status kodundan asılı olmayaraq eyni forma)
{
  statusCode: number,
  message: string,
  errors?: { [field: string]: string[] },  // yalnız validasiya xətalarında
  traceId?: string
}
```

**Status kodları:**
| Kod | Məna |
|---|---|
| `400` | Sorğu formatı/biznes qaydası səhvdir → `errors` obyektinə bax |
| `401` | Token yoxdur/etibarsızdır/bitib → refresh cəhd et, alınmasa login-ə yönləndir |
| `403` | Token düzgündür, amma icazə yoxdur (rol/sahiblik) |
| `404` | Resurs tapılmadı |
| `409` | Konflikt (dublikat, artıq mövcud, yer bitib) |
| `500` | Server xətası (dev-də detal, prod-da ümumi mesaj) |

**Tövsiyə olunan fetch wrapper nümunəsi:**
```ts
async function apiFetch(path: string, options: RequestInit = {}) {
  const res = await fetch(`${BASE}${path}`, {
    ...options,
    headers: {
      ...(options.body && !(options.body instanceof FormData)
        ? { 'Content-Type': 'application/json' }
        : {}),
      ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      ...options.headers,
    },
  });

  if (res.status === 401 && accessToken) {
    const refreshed = await tryRefresh();
    if (refreshed) return apiFetch(path, options); // bir dəfə təkrar cəhd
  }

  if (!res.ok) {
    const error = await res.json().catch(() => null);
    throw new ApiError(res.status, error?.message ?? 'Xəta baş verdi', error?.errors);
  }

  return res.status === 204 ? null : res.json();
}
```

---

## 10. Enum dəyərləri (dəqiq mətn — böyük/kiçik hərfə həssasdır)

| Enum | Dəyərlər |
|---|---|
| `ResourceType` | `WorkSheet`, `Presentation`, `Test`, `MethodGuide`, `Video`, `ExternalLink` |
| `ResourceStatus` | `Pending`, `Approved`, `Rejected` |
| `TrainingFormat` | `Live`, `Online`, `Video` |
| `EnrollmentStatus` | `InProgress`, `Completed` |
| `CatalogItemType` | `Resource`, `Training` |
| `OrderStatus` | `Paid` (demo rejimdə yeganə istifadə olunan) |

---

## 11. Seed test hesabları (yalnız Development)

| E-poçt | Şifrə | Rol |
|---|---|---|
| `admin@mimedu.az` | `Admin123!` | Admin |
| `nigar@mimedu.az` | `Teacher123!` | Teacher |
| `elvin@mimedu.az` | `Teacher123!` | Teacher |
| `sevinc@mimedu.az` | `Student123!` | Student |
| `tural@mimedu.az` | `Student123!` | Student |

Test sertifikat kodu: **`MIM-2026-4417`** (`/certificates/verify/MIM-2026-4417` ilə sınayın).
