# Şagird rolu və təlim dərsləri — dəyişiklik sənədi

**Commit:** `0e0ed23` · **Tarix:** 2026-09-07

Bu sənəd yalnız **bu dəyişikliyi** əhatə edir. Tam API bələdçisi üçün
[`API_FRONTEND.md`](./API_FRONTEND.md), ümumi layihə sənədi üçün [`README.md`](./README.md).

---

## 1. Problem

İstifadəçi təlimi **ala** bilirdi, amma aldıqdan sonra **baxacağı heç nə yox idi**:

| Nə var idi | Nə çatmırdı |
|---|---|
| Səbət → checkout → `Enrollment` yaranırdı | Təlimin dərsləri/videoları yox idi — `TrainingSyllabusItem` yalnız mətn sətri idi |
| `Enrollment.ProgressPercent` sahəsi bazada var idi | Onu **yeniləyən endpoint yox idi** — həmişə `0` qalırdı |
| Resurs imtahanı → avtomatik sertifikat | Təlim üçün belə axın yox idi — sertifikatı yalnız admin əl ilə verirdi |
| Hamı `Teacher` rolu ilə qeydiyyatdan keçirdi | Yalnız təlim alan, material satmayan **şagird hesabı** anlayışı yox idi |

---

## 2. Nə əlavə olundu

### 2.1 Üçüncü rol: `Student`

| Rol | İmkanlar |
|---|---|
| **Student** | Təlim alır, dərslərə baxır, imtahan verir, sertifikat alır, resurs endirir. **Material yükləyə bilmir.** |
| **Teacher** | Şagirdin hər şeyi **+** Resurs Bankına material yükləyib satmaq (80% pay) |
| **Admin** | Hər şey + moderasiya, təlim/dərs idarəetməsi, statistika, audit log |

- Qeydiyyatda `accountType` göndərilməsə **`Student`** yaradılır.
- Şagird sonradan müəllif ola bilər (`POST /auth/become-author`) — `Student` rolu itmir, üstünə `Teacher` əlavə olunur.

### 2.2 Təlim dərsləri (`TrainingLesson`)

Hər dərs: **başlıq + təsvir + video linki + müddət**.

- `videoUrl` **yalnız təlimə yazılmış istifadəçiyə** (və adminə) qaytarılır — başqasına `403`.
- `TrainingSyllabusItem` (ictimai proqram) olduğu kimi qalır — o, satın almadan əvvəl görünən siyahıdır. Dərslər isə qorunan məzmundur.

### 2.3 Dərs-dərs irəliləyiş və avtomatik sertifikat

- İstifadəçi hər dərsi "tamamladım" işarələyir → `progressPercent` **avtomatik hesablanır**.
- **100%-də sertifikat özü yaranır**, `Enrollment` `Completed` statusuna keçir.
- Admin sonradan dərs əlavə edərsə, bütün yazılmış istifadəçilərin faizi **yenidən hesablanır** — köhnə 100% səhv qalmır.

---

## 3. ⚠️ Frontend-i sındıra bilən dəyişikliklər

### 3.1 `POST /resources` artıq rol tələb edir

```
Əvvəl:  [Authorize]                 → hər daxil olmuş istifadəçi
İndi:   [Authorize(Teacher, Admin)] → şagird 403 alır
```

**Nə etməli:** "Material yüklə" düyməsini `user.canPublishResources` sahəsinə görə göstərin.
Şagirdə düymə əvəzinə "Müəllif ol" təklifi göstərin.

### 3.2 `UserDto`-ya yeni sahə

```ts
{
  id, fullName, email, subject,
  roles: string[],                 // ["Student"] | ["Teacher"] | ["Student","Teacher"] | ["Admin"]
  canPublishResources: boolean,    // ⭐ YENİ
  createdAt
}
```

### 3.3 `RegisterRequest`-ə opsional sahə

```ts
{
  fullName, email, password,
  subject?: string,
  accountType?: "Student" | "Teacher"   // ⭐ YENİ — göndərilməsə "Student"
}
```

> **Diqqət:** hazırda qeydiyyat forması "Müəllif hesabı aç" mətni ilə gəlir, amma
> `accountType` göndərmədiyi üçün **şagird** yaradır. Formaya hesab növü seçimi
> əlavə edilməlidir (aşağıda 6-cı bölmə).

### 3.4 Təlim DTO-larına yeni sahələr

```ts
// TrainingDto (GET /trainings)
{ ..., lessonCount: number }                       // ⭐ YENİ

// TrainingDetailDto (GET /trainings/{id})
{ ..., lessonCount: number, isEnrolled: boolean }   // ⭐ YENİ
// isEnrolled yalnız token göndərilibsə mənalıdır; anonim sorğuda həmişə false

// MyTrainingDto (GET /trainings/mine)
{
  ...,
  completedLessonCount: number,   // ⭐ YENİ
  totalLessonCount: number,       // ⭐ YENİ
  completedAt: string | null      // ⭐ YENİ
}
```

Bunlar **əlavə** sahələrdir — mövcud sahələr silinməyib, köhnə kod sınmır.

---

## 4. Yeni endpoint-lər

| Metod | Yol | İcazə |
|---|---|---|
| `POST` | `/api/v1/auth/become-author` | Authorize |
| `GET` | `/api/v1/trainings/{trainingId}/lessons` | Authorize (**yalnız yazılanlar**) |
| `POST` | `/api/v1/trainings/{trainingId}/lessons/{lessonId}/complete` | Authorize |
| `DELETE` | `/api/v1/trainings/{trainingId}/lessons/{lessonId}/complete` | Authorize |
| `POST` | `/api/v1/trainings/{trainingId}/lessons` | Admin |

### `POST /auth/become-author` 🔒

Şagird hesabını müəllif hesabına yüksəldir.

```ts
// Request
{ subject?: string }

// 200 OK — UserDto
{ roles: ["Student", "Teacher"], canPublishResources: true, ... }
```

> ⚠️ **Vacib:** yeni rol **mövcud access token-də olmur**. Bu çağırışdan sonra mütləq
> `POST /auth/refresh` edin — əks halda `POST /resources` hələ də `403` verəcək.

### `GET /trainings/{trainingId}/lessons` 🔒

```ts
// 200 OK — TrainingLessonsDto
{
  trainingId: string,
  trainingName: string,
  lessons: [{
    id: string,
    orderIndex: number,             // 0-dan başlayır, sıralı gəlir
    title: string,
    description: string,
    videoUrl: string | null,        // YouTube/Vimeo linki
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

Xətalar: `401` (token yoxdur), `403` (təlimə yazılmayıb), `404` (təlim yoxdur).

### `POST /trainings/{trainingId}/lessons/{lessonId}/complete` 🔒

```ts
// 200 OK — LessonProgressDto
{
  trainingId: string,
  lessonId: string,
  isCompleted: boolean,
  completedLessonCount: number,
  totalLessonCount: number,
  progressPercent: number,          // avtomatik hesablanır
  status: "InProgress" | "Completed",
  certificateCode: string | null    // ⭐ yalnız 100%-də dolur
}
```

- Eyni dərsi təkrar göndərmək faizi **artırmır** (idempotent).
- `DELETE` eyni yola — "tamamlandı" işarəsini geri götürür, eyni DTO qayıdır.

### `POST /trainings/{trainingId}/lessons` 🔒 (Admin)

```ts
// Request
{
  mode: "append" | "replace",
  lessons: [{
    title: string,
    description: string,
    videoUrl?: string,          // tam URL olmalıdır (http:// və ya https://)
    durationMinutes?: number    // 1–600
  }]
}
// 200 OK — TrainingLessonsDto
```

`POST /trainings` (təlim yaratma) da artıq `lessons` massivini qəbul edir.
`syllabus` boş göndərilsə, dərs başlıqlarından avtomatik doldurulur.

---

## 5. Tam axın

```
GET  /trainings/{id}                            → isEnrolled: false, lessonCount: 4
POST /cart/items { itemType:"Training", itemId } → səbətə
POST /orders/checkout                            → Enrollment yaranır
GET  /trainings/{id}                             → isEnrolled: true  ("Dərslərə keç" düyməsi)
GET  /trainings/{id}/lessons                     → videolar açılır
POST /trainings/{id}/lessons/{lessonId}/complete → hər dərsdən sonra
        └─ sonuncu dərsdə → certificateCode qayıdır
GET  /certificates/verify/{code}                 → publik doğrulama
```

---

## 6. Frontend üçün iş siyahısı

- [ ] **Qeydiyyat formasına hesab növü seçimi** — "Təlim almaq istəyirəm" (Student) / "Material satmaq istəyirəm" (Teacher). Seçim `accountType` sahəsində göndərilir.
- [ ] **"Material yüklə" düyməsini gizlət** — `user.canPublishResources === false` olanda göstərmə, əvəzinə "Müəllif ol" təklifi göstər.
- [ ] **"Müəllif ol" axını** — `POST /auth/become-author` → **dərhal** `POST /auth/refresh` → yenilənmiş token və `user`-i saxla.
- [ ] **Təlim detal səhifəsi** — `isEnrolled` true isə "Dərslərə keç", false isə "Səbətə at". `lessonCount`-u göstər.
- [ ] **Dərs səhifəsi (yeni)** — dərs siyahısı, video player (`videoUrl`), hər dərsdə "Tamamladım" checkbox, yuxarıda `progressPercent` progress bar.
- [ ] **Sertifikat bildirişi** — `complete` cavabında `certificateCode` gələndə təbrik modalı göstər.
- [ ] **"Mənim təlimlərim"** — `completedLessonCount / totalLessonCount` göstər (məs. "3/6 dərs").

---

## 7. Test hesabları (yalnız Development)

| E-poçt | Şifrə | Rol |
|---|---|---|
| `sevinc@mimedu.az` | `Student123!` | Student |
| `tural@mimedu.az` | `Student123!` | Student |
| `nigar@mimedu.az` | `Teacher123!` | Teacher |
| `admin@mimedu.az` | `Admin123!` | Admin |

Seed təlimləri və dərs sayları:

| Təlim | Id | Dərs |
|---|---|---|
| Süni intellektlə dərs dizaynı | `4444...4441` | 4 |
| Formativ qiymətləndirmə praktikası | `4444...4442` | 6 |
| Sinif idarəetməsi: video kurs | `4444...4443` | 4 |

> Video linkləri seed-də **nümunə (placeholder)** linklərdir — real video yoxdur.
> Admin `POST /trainings/{id}/lessons` ilə (`mode: "replace"`) real linklərlə əvəz edə bilər.

---

## 8. Verilənlər bazası

Migration: `20260907183612_AddTrainingLessonsAndStudentRole` — Supabase-ə **tətbiq olunub**.

| Cədvəl | Təyinat |
|---|---|
| `training_lessons` | Dərslər (başlıq, təsvir, video linki, müddət, sıra) |
| `lesson_completions` | Kim hansı dərsi nə vaxt tamamladı (unikal: enrollment + dərs) |
| `enrollments.CompletedAt` | Yeni sütun — təlimin 100% tamamlandığı an |

Mövcud 3 seed təliminə dərslər avtomatik dolduruldu (14 dərs). Seeder idempotentdir —
təlimdə artıq dərs varsa toxunmur.

---

## 9. Yoxlama nəticələri

| Test | Nəticə |
|---|---|
| `scripts/student-flow-test.mjs` (yeni, 41 assertion) | **41/41** |
| `scripts/smoke-test.mjs` (mövcud, 100 assertion) | **100/100** |
| Unit testlər | **52/52** |

Yeni test skripti bu ssenariləri əhatə edir: default rolun şagird olması, şagirdin material
yükləyə bilməməsi (403), yazılmadan dərslərin görünməməsi (403), alışdan sonra videoların
açılması, faizin dərs-dərs artması, təkrar tamamlamanın faizi artırmaması, işarənin geri
götürülməsi, 100%-də sertifikatın avtomatik verilməsi və şagirdin müəllifə yüksəlməsi.

```bash
node scripts/student-flow-test.mjs
```

---

## 10. Yol boyu tapılan bug

Dərs tamamlananda **faiz iki dəfə sayılırdı** (1 dərs → 50%, 3 dərs → 100%).

**Səbəb:** `_db.LessonCompletions.Add(...)` çağırışı EF Core-un relationship fixup
mexanizmi ilə `enrollment.CompletedLessons` kolleksiyasını **artıq yeniləyirdi**, kod isə
üstünə əl ilə `+1` edirdi. Nəticədə təlim vaxtından əvvəl "tamamlandı" sayılır və sertifikat
75%-də verilirdi.

Yeni test skripti bunu tutdu; say indi birbaşa kolleksiyadan götürülür.
