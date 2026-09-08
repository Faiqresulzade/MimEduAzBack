# Video/xarici link resursları və sertifikat sənədi — dəyişiklik sənədi

**Commit:** `2789420` · **Tarix:** 2026-09-08

Bu sənəd yalnız **bu dəyişikliyi** əhatə edir. Tam API bələdçisi üçün
[`API_FRONTEND.md`](./API_FRONTEND.md), əvvəlki dəyişiklik üçün
[`CHANGELOG_STUDENT_LEARNING.md`](./CHANGELOG_STUDENT_LEARNING.md).

---

## 1. Nə əlavə olundu

| # | Nə | Necə |
|---|---|---|
| 1 | **Video dərs tipi** | Fayl yüklənmir, YouTube/Vimeo linki saxlanılır. Ödənişli ola bilər. |
| 2 | **Xarici resurs tipi** | Başqa saytda hazırlanmış material (Wordwall, Canva, Drive...). Yalnız pulsuz. |
| 3 | **Sertifikat sənədi** | A4 landşaft PNG və PDF, QR kod və MIMEDU.AZ imzası ilə, endirilə bilən. |

---

## 2. Yeni resurs tipləri

`ResourceType` enum-una iki dəyər əlavə olundu:

| Tip | Fayl lazımdır? | Ödənişli ola bilər? | Nə üçün |
|---|---|---|---|
| `Video` | ❌ Xeyr, link | ✅ **Bəli** | YouTube/Vimeo video dərsi |
| `ExternalLink` | ❌ Xeyr, link | ❌ **Yalnız pulsuz** | Başqa saytdakı interaktiv material |

> **Niyə `ExternalLink` ödənişli ola bilmir:** link bir dəfə paylaşıldıqdan sonra ona
> nəzarət etmək mümkün deyil — alıcı linki başqasına verə bilər. Ödənişli satış üçün
> video dərs (linki gizlədilir) və ya fayl yükləmə istifadə olunur.

### `POST /api/v1/resources/link` 🔒 (yalnız Teacher / Admin)

**JSON**-dur, `multipart/form-data` deyil.

```ts
// Request
{
  name: string,                       // maks. 200
  subject: string,
  grade: number,                      // 1–11
  type: "Video" | "ExternalLink",
  externalUrl: string,                // tam URL: http:// və ya https://
  isPaid?: boolean,                   // ExternalLink-də mütləq false
  price?: number                      // isPaid=true olanda >0, maks. 1000
}

// 201 Created — ResourceDetailDto (status: "Pending")
```

**Xətalar:**

| Kod | Səbəb |
|---|---|
| `400` | Səhv/natamam URL, ödənişli `ExternalLink`, fayl tipi (`WorkSheet` və s.) göndərilib |
| `403` | Şagird (`Student`) hesabı — yalnız `Teacher`/`Admin` yükləyə bilər |

Digər resurslar kimi **moderasiyaya düşür** (`Pending`) — admin təsdiqləyənə qədər
Resurs Bankında görünmür.

### Fayl endpoint-i ilə ayrılma

`POST /resources` (multipart) artıq `Video`/`ExternalLink` tiplərini **qəbul etmir** (400),
`POST /resources/link` isə fayl tiplərini qəbul etmir (400). Hər tip öz endpoint-indən keçir.

---

## 3. ⚠️ Frontend-i sındıra bilən dəyişikliklər

### 3.1 `ResourceDto` (siyahı) — yeni sahə

```ts
{
  ...,
  isLinkBased: boolean    // ⭐ YENİ — Video / ExternalLink olduqda true
}
```

### 3.2 `ResourceDetailDto` — iki yeni sahə

```ts
{
  ...,
  isLinkBased: boolean,        // ⭐ YENİ
  externalUrl: string | null   // ⭐ YENİ — aşağıya bax
}
```

**`externalUrl` nə vaxt dolu gəlir:**

| Hal | `externalUrl` |
|---|---|
| Pulsuz video / xarici link | ✅ **Dolu** — dərhal embed edə bilərsiniz |
| **Ödənişli** video (satın alınmayıb) | ❌ **`null`** — link gizlədilir |
| Fayl əsaslı resurs | `null` (linki yoxdur) |

### 3.3 `ResourceDownloadDto` — yeni sahə

```ts
{
  resourceId, fileName,
  downloadUrl: string,     // fayl yolu VƏ YA xarici link
  isExternal: boolean,     // ⭐ YENİ
  downloads: number
}
```

**`isExternal` necə emal olunmalıdır:**

```ts
const res = await apiFetch(`/resources/${id}/download`, { method: 'POST' });

if (res.isExternal) {
  window.open(res.downloadUrl, '_blank', 'noopener');   // YouTube/Wordwall — yeni tab
} else {
  window.location.href = `${API_ORIGIN}${res.downloadUrl}`;  // fayl — endirmə
}
```

### 3.4 `type` filtri genişləndi

`GET /resources?type=...` indi `Video` və `ExternalLink` dəyərlərini də qəbul edir.

---

## 4. Ödənişli videonun axını

```
GET  /resources/{id}                → externalUrl: null  ("Satın al" düyməsi)
POST /cart/items { Resource, id }   → səbətə
POST /orders/checkout               → ödəniş (demo)
POST /resources/{id}/download       → { downloadUrl: "https://youtube.com/...",
                                        isExternal: true }
```

Satın almadan `download` çağırılsa → **403**.

Pulsuz videoda bu addımlar lazım deyil — `externalUrl` birbaşa detal cavabındadır.

---

## 5. Sertifikat sənədi

### `GET /api/v1/certificates/{code}/download` — **publik**

Doğrulama endpoint-i kimi token tələb etmir: işəgötürən kodu bilirsə sertifikatı
yükləyib yoxlaya bilər.

| Query | Dəyər | Nəticə |
|---|---|---|
| `format` | `png` (default) | `image/png`, A4 landşaft, 150 DPI (~90 KB) |
| `format` | `pdf` | `application/pdf`, çap üçün |

```
GET /api/v1/certificates/MIM-2026-4417/download             → PNG
GET /api/v1/certificates/MIM-2026-4417/download?format=pdf  → PDF
```

Cavab **fayl axınıdır** (`Content-Disposition: attachment`), JSON deyil.
Kod böyük/kiçik hərfə həssas deyil. Mövcud olmayan kod → `404` (standart xəta formatı).

### Frontend nümunəsi

```tsx
// Endirmə düyməsi
<a href={`${BASE}/certificates/${code}/download?format=pdf`} download>
  PDF endir
</a>

// Səhifədə göstərmək
<img src={`${BASE}/certificates/${code}/download`} alt="Sertifikat" />
```

### Sertifikatın üzərində nə var

- MIMEDU.AZ loqosu və alt yazı
- «SERTİFİKAT» başlığı, qızılı ikiqat haşiyə
- Sahibin adı, təlimin adı, saat sayı, verilmə tarixi
- **Sol aşağı:** doğrulama ünvanına aparan QR kod + sertifikat kodu
- **Sağ aşağı:** stilizasiya olunmuş MIMEDU.AZ imzası + «Platforma rəhbərliyi»

Nümunə: [`docs/sertifikat-numune.png`](./docs/sertifikat-numune.png)

### ⚠️ Frontend-də olmalı olan marşrut

QR kod bu ünvana aparır:

```
https://mimedu.az/sertifikat-yoxla/{kod}
```

**Bu səhifə frontend-də mövcud olmalıdır** — əks halda QR skan edən 404 alacaq.
Səhifə `GET /api/v1/certificates/verify/{code}` çağırıb nəticəni göstərməlidir.

Ünvanı dəyişmək istəsəniz Render-də env dəyişəni:
```
Certificate__VerificationUrlTemplate=https://mimedu.az/basqa-yol/{code}
```

Digər tənzimləmələr: `Certificate__SignatureName`, `Certificate__SignatureTitle`,
`Certificate__PngDpi`.

---

## 6. Sualınıza cavab: `confirmPassword` var?

**Xeyr — backend-də yoxdur və olmamalıdır.**

`POST /auth/register` yalnız bu sahələri qəbul edir:

```ts
{ fullName, email, password, subject?, accountType? }
```

**Səbəb:** «şifrəni təkrar yaz» sahəsi istifadəçinin **yazı səhvini tutmaq üçün UI
vasitəsidir** — serverə eyni dəyəri iki dəfə göndərməyin heç bir təhlükəsizlik faydası
yoxdur. Ona görə standart yanaşma budur ki, uyğunluq **frontend-də** yoxlanılır və
backend-ə yalnız bir `password` göndərilir.

**Praktik qeyd:** əgər frontend `confirmPassword` sahəsini də göndərsə, **xəta olmayacaq** —
backend tanımadığı JSON sahələrini sükutla nəzərə almır. Yəni mövcud formanızı
dəyişmədən də işləyəcək; sadəcə uyğunluq yoxlamasını göndərməzdən əvvəl özünüz edin.

### Backend-in şifrə qaydaları (frontend validasiyası bunlarla üst-üstə düşməlidir)

| Qayda | Dəyər |
|---|---|
| Minimum uzunluq | **8 simvol** |
| Ən azı bir rəqəm | ✅ Tələb olunur |
| Ən azı bir kiçik hərf | ✅ Tələb olunur |
| Böyük hərf | ❌ Tələb olunmur |
| Xüsusi simvol | ❌ Tələb olunmur |
| E-poçt təkrarı | ❌ Qadağan (`409` qaytarır) |

Qayda pozulanda `400` + sahə üzrə mesajlar:

```json
{
  "statusCode": 400,
  "message": "Göndərilən məlumatlar düzgün deyil.",
  "errors": {
    "password": ["Şifrə ən azı 8 simvol olmalıdır.", "Şifrədə ən azı bir rəqəm olmalıdır."]
  }
}
```

> Əgər buna baxmayaraq backend-də də `confirmPassword` yoxlanışı istəyirsinizsə,
> deyin — 5 dəqiqəlik işdir, amma tövsiyə etmirəm.

---

## 7. Frontend üçün iş siyahısı

- [ ] **Material yükləmə formasına tip seçimi** — indi 4 tip var idi, indi 6: fayl tipləri
      (`WorkSheet`, `Presentation`, `Test`, `MethodGuide`) → fayl input; `Video`/`ExternalLink`
      → link input. Tip seçiminə görə forma dəyişməlidir.
- [ ] **İki fərqli endpoint** — fayl tiplərində `POST /resources` (multipart),
      link tiplərində `POST /resources/link` (JSON).
- [ ] **`ExternalLink` seçiləndə qiymət sahəsini gizlət/söndür** (yalnız pulsuz ola bilər).
- [ ] **Resurs kartında `isLinkBased`** — "Endir" əvəzinə "Aç"/"İzlə" düyməsi göstər.
- [ ] **`isExternal` emalı** — `window.open(..., '_blank')`, endirmə yox.
- [ ] **Pulsuz videonu embed et** — `externalUrl` dolu gələndə YouTube/Vimeo player göstər.
- [ ] **Ödənişli videoda "Satın al"** — `externalUrl === null` olanda.
- [ ] **Sertifikat endirmə düymələri** — PNG və PDF (sertifikatlarım səhifəsi + təlim
      tamamlandıqdan sonrakı təbrik ekranı).
- [ ] **`/sertifikat-yoxla/:kod` marşrutu** — QR kodun apardığı səhifə.

---

## 8. Yoxlama nəticələri

| Test | Nəticə |
|---|---|
| `scripts/link-and-certificate-test.mjs` (yeni, 32 assertion) | **32/32** |
| Unit testlər (8 yeni sertifikat renderi testi) | **60/60** |
| `scripts/smoke-test.mjs` | **100/100** |
| `scripts/student-flow-test.mjs` | **41/41** |

Migration `20260908170222_AddLinkResourcesAndCertificateDocs` Supabase-ə tətbiq olunub.

---

## 9. Texniki qeydlər

**Şrift:** sertifikat renderi üçün **Noto Sans** layihəyə əlavə edildi (SIL OFL,
sərbəst paylanır). Səbəb: sistem şriftlərində Azərbaycan **«ə»** hərfi (U+0259) çox
vaxt olmur, Linux konteynerində isə ümumiyyətlə şrift yoxdur. Hər iki şriftdə
`ə ş ğ ı ö ü ç Ə İ` hərflərinin varlığı proqramla yoxlanılıb.

**Kitabxanalar:** QuestPDF (PNG və PDF eyni layout-dan), QRCoder (QR kod).

**Yol boyu tapılan qüsur:** uzun təlim adı sertifikatı 2 səhifəyə bölürdü və yalnız
1-ci səhifə render olunduğu üçün **QR kod və imza sətri sükutla itirdi**. Haşiyə fon
qatına, alt sətir `Footer` slotuna keçirildi, uzun mətnlər kəsilir. «Bir səhifə»
qaydası ayrıca testlə qorunur.
