# Dəyişiklik siyahısı — KSQ Cavab Kartı Yoxlama

Müəllim KSQ testinin açarını (A/B variant) bir dəfə qeyd edir, şagird kartlarının şəklini
çəkir — vision model (OpenRouter) cavabları oxuyur, bal avtomatik hesablanır, nəticələr
saxlanır və CSV kimi yüklənir. Frontend: `/panel/ksq-yoxlama` (müəllim paneli + header linki).

---

## 1. Qəbul edilmiş qərarlar

| Sual | Qərar |
|---|---|
| Kim istifadə edir | **Teacher + Admin** (`AuthorRoles`). Şagird 403 alır. |
| Məlumatın sahibi | Hər test və nəticə `UserId`-yə bağlıdır; başqasının qeydi `404` kimi görünür. |
| Test silinəndə | **Nəticələri də silinir** (cascade). |
| Massivlər | PostgreSQL **`jsonb`** sütunları (ballar, açarlar, cavablar, səhv suallar). |
| Bal | Server client-in balına güvənmir — saxlayanda açara görə **yenidən hesablayır**. |
| Şəkil | **Saxlanmır** — nə diskə, nə bazaya, nə loga. Yaddaşda oxunur, OpenRouter-ə gedir, atılır. |
| AI modeli | `anthropic/claude-haiku-5.5`, ehtiyat: pulsuz `nvidia/nemotron-3.5-super-vl:free` (`OpenRouter:VisionModel`, `OpenRouter:FallbackModels`). 23 modelin benchmark-ında yalnız bu ikisi və gemini-3.7-flash 100% verdi; Haiku 1000 kart ≈ $0.5. |
| Limit | Müəllim başına **dəqiqədə 20, sutkada 500** şəkil (`429`). Timeout **60 s** (`504`). |

Orijinal artifact HTML-i əlçatan olmadığı üçün prompt, JSON sxemi, CSV formatı və UI mətnləri
tapşırıqdakı spesifikasiya əsasında yazılıb.

---

## 2. Verilənlər bazası

Migration: `AddKsq` — `ksq_tests`, `ksq_results`. Tətbiq qalxanda avtomatik tətbiq olunur.

## 3. Endpoint-lər (`[Authorize(Roles = "Teacher,Admin")]`)

| Metod | Yol | Qeyd |
|---|---|---|
| GET | `/api/v1/ksq/tests` | Öz testləri |
| POST / PUT | `/api/v1/ksq/tests[/{id}]` | Ad, `closedPoints`, `openPoints`, `keyA`, `keyB` |
| DELETE | `/api/v1/ksq/tests/{id}` | Nəticələri ilə birlikdə |
| GET | `/api/v1/ksq/results?testId=&sinif=` | `{ items, classes }` |
| POST | `/api/v1/ksq/results` | Bir nəticə; bal serverdə |
| POST | `/api/v1/ksq/results/bulk` | "Hamısını saxla" — hamısı və ya heç biri |
| DELETE | `/api/v1/ksq/results/{id}` | |
| POST | `/api/v1/ksq/scan` | multipart: `image`, `closedCount`, `openCount` |

Nəticə sahələri: `id, test, testId, sinif, ad, variant, kod, cavablar[], aciq[], bal, max, sehv[], ts`.

Scan xətaları: `400` (format/ölçü), `429` (limit), `502` (AI səhv cavab), `503` (açar yoxdur /
balans bitib), `504` (60 s keçdi).

## 4. Konfiqurasiya

```bash
# Lokal
dotnet user-secrets set "OpenRouter:ApiKey" "<açar>" --project src/MimeduAz.Api
```

Production (Render və s.): env dəyişəni **`OPENROUTER_API_KEY`** (və ya `OpenRouter__ApiKey`).
Açar yoxdursa scan `503` qaytarır, qalan hər şey işləyir — müəllim cavabları əl ilə qeyd edə bilər.

## 5. Test

- Unit: `KsqServiceTests` (bal, sahiblik, cascade, toplu saxlama, limit, parser).
- Uçdan-uca: `node scripts/ksq-test.mjs [kart.jpg]` — şəkil verilsə real OpenRouter oxuması da yoxlanır.
