# Dəyişiklik siyahısı — Sınaq (imtahan) modulu

Müəllimlər təlimlər kimi **sınaq yaradıb sata bilirlər**. Sınağın vaxt limiti var,
fənn bölmələrinə ayrılır və keçid balından yuxarı nəticəyə **sertifikat verilir**.

---

## 1. Qəbul edilmiş qərarlar

| Sual | Qərar |
|---|---|
| Sınağın "vaxtı" | **Yalnız müddət (vaxt limiti)** — təqvim tarixi yoxdur. Sayğac iştirakçı sınağa başladığı andan işləyir. |
| Moderasiya | **Var** — resurslar kimi: müəllim yaradır → `Pending` → admin təsdiqləyir → satışa çıxır. |
| Sertifikat | **Yalnız keçid balından yuxarı.** Keçid faizini müəllim özü təyin edir. |
| Düstur/qrafik olan suallar | **Mətn + opsional şəkil.** Mətndə LaTeX (`$...$`) saxlanıla bilər (render frontend-dədir), mürəkkəb suallar üçün şəkil yüklənir. |
| Struktur | **Fənn bölmələri ilə** — nəticədə həm ümumi, həm hər fənn üzrə ayrıca bal. |

**Əlavə qaydalar** (təsdiq üçün — dəyişmək asandır):
- Komissiya resurslardakı kimi **20%** (`Commission:ExamPercent`).
- **Bir sınaq = bir cəhd.** Satın alınan sınaq bir dəfə verilir.
- Vaxt bitəndən sonra göndərilən cavab **qəbul olunmur** → cəhd `Expired`, bal 0, sertifikat yox.
  Şəbəkə gecikməsi üçün **30 saniyə güzəşt** var.
- Nəticə dərhal görünür: ümumi bal + fənn üzrə bal + **cavab açarı** (hansı sualı səhv etdiyi).
- Pulsuz sınaq olar. Müəllif öz sınağını nə ala, nə də verə bilər.
- Məzmun (suallar) dəyişəndə təsdiqlənmiş sınaq **yenidən moderasiyaya düşür**.
- Kimsə sınağı veribsə sualları dəyişmək və sınağı silmək olmur (`409`).

---

## 2. Verilənlər bazası

**Yeni cədvəllər (5):**

| Cədvəl | Təyinat |
|---|---|
| `exams` | Sınaq: ad, fənn, sinif, müəllif, `duration_minutes`, `pass_percent`, qiymət, moderasiya statusu |
| `exam_sections` | Fənn bölməsi (məs. "Riyaziyyat") |
| `exam_questions` | Sual: mətn (LaTeX ola bilər) + opsional `image_path` + `options text[]` + düzgün indeks |
| `exam_attempts` | Cəhd: `started_at`, **`expires_at`** (server tərəfli sayğac), nəticə, status |
| `exam_answers` | Cəhdin hər sual üzrə cavabı — fənn üzrə bal və cavab açarı üçün |

**Mövcud sxemdə dəyişiklik:**
- `certificates.exam_attempt_id` (nullable FK) — sertifikat artıq təlim, resurs imtahanı
  və **sınaq** cəhdinə bağlana bilir.
- `CatalogItemType` enum-una **`Exam`** dəyəri əlavə olundu (səbət/sifariş sətirləri).
  Enum bazada **mətn** kimi saxlandığı üçün mövcud sətirlər təsirlənmir.

**Migration:** `20260919070424_AddExams`.
Tətbiq Render-də qalxarkən migration-ları **avtomatik icra edir** (`Program.ApplyMigrationsAsync`),
ona görə Supabase-ə əlavə əl əməliyyatı lazım deyil — növbəti deploy-da özü tətbiq olunacaq.

---

## 3. Endpoint-lər

### Kataloq (publik)
| Endpoint | Qeyd |
|---|---|
| `GET /exams` | Filtr: `subject`, `grade`, `isPaid`, `search`, `page`, `pageSize` (+ `status` — yalnız Admin) |
| `GET /exams/{id}` | Bölmələr və hər bölmədəki sual sayı. **Sualların özü burada yoxdur.** |

### Müəllim (Teacher / Admin)
| Endpoint | Qeyd |
|---|---|
| `POST /exams` | Bölmə və suallarla birlikdə yaradır → `Pending` |
| `PUT /exams/{id}` | Meta məlumatlar (suallara toxunmur). Rədd edilmiş sınaq yenidən `Pending` olur |
| `PUT /exams/{id}/sections` | Bütün bölmə/sualları əvəz edir. Cəhd varsa `409`; təsdiqlənmişsə yenidən `Pending` |
| `DELETE /exams/{id}` | Cəhd varsa `409`. Şəkillər də diskdən silinir |
| `POST /exams/{id}/images` | **Sual şəkli yükləmə** (multipart `file`) → `{ imagePath, imageUrl }` |
| `GET /exams/mine` | Müəllimin öz sınaqları (bütün statuslar) |

### İştirakçı
| Endpoint | Qeyd |
|---|---|
| `POST /exams/{id}/start` | Cəhd yaradır, sualları qaytarır + `expiresAt`/`remainingSeconds`. Təkrar çağırış **eyni cəhdi davam etdirir**, vaxt sıfırlanmır |
| `POST /exams/attempts/{attemptId}/submit` | Qiymətləndirir, keçibsə sertifikat verir. Vaxt bitibsə `409` |
| `GET /exams/attempts/{attemptId}` | Nəticə: fənn üzrə bal + cavab açarı |
| `GET /exams/attempts/mine` | Bütün cəhdlərim |
| `GET /exams/purchased` | Satın aldığım sınaqlar |

### Admin moderasiya
| Endpoint | Qeyd |
|---|---|
| `GET /admin/exams/pending` | Növbə — bölmə və sual sayı ilə |
| `POST /admin/exams/{id}/approve` | Təsdiq |
| `POST /admin/exams/{id}/reject` | `{ reason? }` |

---

## 4. Düsturlu suallar necə işləyir

Müəllimin üç seçimi var, üçü də eyni data modeli ilə işləyir:

1. **Sadə mətn** — `questionText: "200-ün 15%-i neçədir?"`
2. **LaTeX** — `questionText: "$x^2 = 49$ tənliyinin müsbət kökü nədir?"`.
   Backend mətni olduğu kimi saxlayır; **frontend KaTeX/MathJax ilə render etməlidir**.
   Variantlar da LaTeX ola bilər: `["$a^2 + b^2$", "$a^2 + 2ab + b^2$", ...]`.
3. **Şəkil** — `POST /exams/{id}/images` ilə şəkil yüklənir, qayıdan `imagePath`
   sualın `imagePath` sahəsinə yazılır. Qrafik, cədvəl və mürəkkəb düsturlar üçün.
   Bu halda `questionText` boş qala bilər, variantlar `A/B/C/D` kimi verilə bilər.

Validasiya: **sual ya mətn, ya şəkil ehtiva etməlidir** — ikisi də boş ola bilməz.

Şəkil limitləri (`FileStorage` bölməsi): `uploads/exams` qovluğu, **max 5 MB**,
`.jpg/.jpeg/.png/.webp`.

---

## 5. Vaxt idarəetməsi

Sayğac **server tərəfdə** saxlanılır — brauzerin saatı ilə oynamaq mümkün deyil:

- `POST /exams/{id}/start` cəhd yaradır və `expires_at = now + durationMinutes` yazır.
- Cavabda `expiresAt` (mütləq an) və `remainingSeconds` (qalan saniyə) gəlir —
  **sayğac `remainingSeconds`-dən qurulmalıdır**, lokal saatdan yox.
- Səhifə yenilənsə `start` təkrar çağırıla bilər: eyni cəhd qaytarılır, vaxt sıfırlanmır.
- Sayğac sıfıra çatanda frontend avtomatik `submit` göndərməlidir.
- Serverdə `expiresAt + 30 san` güzəştdən sonra gələn cavab qəbul olunmur:
  cəhd `Expired` olur, bal 0, sertifikat verilmir (`409`).

---

## 6. Sertifikat

Keçid balından yuxarı nəticədə sertifikat **avtomatik** verilir və mövcud sertifikat
infrastrukturuna tam inteqrasiya olunur:
- `GET /certificates/mine` siyahısında görünür
- `GET /certificates/verify/{code}` ilə publik doğrulanır
- `GET /certificates/{code}/download?format=png|pdf` ilə A4 sənəd endirilir

Sənəddəki mətn: *«Sınaq adı» sınağında 85% nəticə göstərdiyinə görə*.
Təsvir formatı: `Ad Soyad · «Sınaq adı» sınağı · 85% · 19.09.2026`.

---

## 7. Admin icmalı

`GET /admin/dashboard` cavabına **`exams`** bloku əlavə olundu:

```ts
exams: {
  totalExams, pendingExams, approvedExams, rejectedExams,
  totalQuestions,
  totalAttempts, attemptsInProgress, passedAttempts,
  averageScorePercent,
  soldExams, examRevenue
}
```

---

## 8. Yoxlama nəticələri

| Test | Nəticə |
|---|---|
| Unit testlər (14 yeni `ExamServiceTests` — vaxt bitməsi, güzəşt, fənn üzrə bal) | **82/82** |
| `scripts/exam-test.mjs` (yeni, 101 assertion) | **101/101** |
| `scripts/smoke-test.mjs` | **100/100** |
| `scripts/student-flow-test.mjs` | **41/41** |
| `scripts/link-and-certificate-test.mjs` | **33/33** |
| `scripts/admin-and-purchases-test.mjs` | **64/64** |

Hər skript **təmiz (sıfırdan seed edilmiş) baza** ilə ayrıca işə salınıb.

**Seed data (yalnız Development):** 2 sınaq —
pulsuz «Riyaziyyat — sürətli sınaq» (10 dəq, 3 sual, biri LaTeX) və
ödənişli «Buraxılış sınağı — I mərhələ» (90 dəq, Riyaziyyat + Fizika bölmələri, 9.90 AZN).

---

## 9. Yol boyu tapılan qüsur

`PUT /exams/{id}/sections` ilk versiyada **500** qaytarırdı. Səbəb layihədə əvvəllər də
rast gəlinən EF davranışıdır: `Guid` açarı əvvəlcədən dolu olan entity yalnız naviqasiya
kolleksiyasına əlavə ediləndə, **artıq izlənən** valideyn altında EF onu `Added` yox
`Modified` kimi görür → `UPDATE` 0 sətrə toxunur → `DbUpdateConcurrencyException`.
Yaratma axınında problem görünmürdü, çünki orada valideynin özü də `Added` idi.
Həll: bölmə və suallar açıq şəkildə `_db.ExamSections.Add(...)` / `_db.ExamQuestions.Add(...)`
ilə əlavə olunur, naviqasiya kolleksiyası isə relationship fixup ilə dolur.

---

## 10. Frontend üçün qeydlər

- **Sual render**: `questionText` içində `$...$` gələ bilər → KaTeX/MathJax lazımdır.
  `imageUrl` doludursa şəkli göstərin (mətnlə birlikdə ola bilər).
- **Sayğac**: `remainingSeconds`-dən qurun, `expiresAt`-i yalnız yoxlama üçün saxlayın.
  Sıfıra çatanda avtomatik `submit` göndərin.
- **Səhifə yenilənməsi**: `start`-ı təkrar çağırmaq təhlükəsizdir — eyni cəhd qayıdır.
- **`canAccess`**: `false`-dursa "Səbətə at", `true`-dursa "Sınağa başla" göstərin.
- **`myAttempt`** (`GET /exams/{id}` cavabında): doludursa istifadəçi sınağı artıq
  verib/başlayıb — `status`-a görə "Davam et" və ya "Nəticəyə bax" göstərin.
- **Nəticə ekranı**: `sections[].scorePercent` ilə fənn üzrə diaqram qurun;
  `questions[].correctOptionIndex` və `selectedIndex` ilə cavab açarını göstərin.
- **Şəkil yükləmə axını**: əvvəlcə `POST /exams/{id}/images` → `imagePath` alın →
  sual formasında həmin dəyəri göndərin. Sınaq hələ yaradılmayıbsa əvvəlcə sınağı
  (minimal bir sualla) yaradın, sonra `PUT /exams/{id}/sections` ilə tam siyahını göndərin.
