# MIMEDU.AZ — Backend

Azərbaycan müəllimləri üçün rəqəmsal təhsil platformasının backend API-si.
ASP.NET Core 8 Web API + PostgreSQL + EF Core (Code-First), JWT autentifikasiya.

> **Ödəniş barədə:** checkout tam **demo/mock** rejimdədir. Backend heç bir kart məlumatı
> qəbul etmir və saxlamır; sifariş birbaşa `Paid` statusunda yaradılır.

---

## Tələblər

| Alət | Versiya |
|---|---|
| .NET SDK | 8.0+ |
| PostgreSQL | 14+ (və ya Docker) |
| `dotnet-ef` | 8.0.8 (`dotnet tool update --global dotnet-ef --version 8.0.8`) |

---

## Sürətli başlanğıc

```bash
# 1. PostgreSQL-i qaldır (Docker ilə) — host portu 5434
docker compose up -d

# 2. API-ni işə sal (migration + seed avtomatik tətbiq olunur)
dotnet run --project src/MimeduAz.Api

# 3. Swagger: http://localhost:5297/swagger
```

> Konteyner **5434** host portuna bağlanır ki, maşında artıq işləyən başqa
> PostgreSQL (məs. 5432-dəki digər layihə konteyneri) ilə toqquşmasın.
> Standart 5432-dən istifadə etmək istəyirsinizsə `docker-compose.yml` və
> `appsettings.Development.json`-dakı portu dəyişin.

### Uçdan-uca smoke test

API işlədiyi halda bütün axını (giriş → səbət → demo checkout → komissiya →
imtahan → sertifikat → moderasiya → fayl yükləmə) yoxlayır:

```bash
node scripts/smoke-test.mjs
```

> Skript təzə seed data gözləyir. Təkrar icradan əvvəl bazanı sıfırlayın:
> ```bash
> docker exec mimedu-postgres psql -U postgres -d postgres -c "DROP DATABASE IF EXISTS mimedu_dev WITH (FORCE);"
> docker exec mimedu-postgres psql -U postgres -d postgres -c "CREATE DATABASE mimedu_dev;"
> ```

Development mühitində tətbiq qalxarkən `Database.MigrateAsync()` və `DataSeeder`
avtomatik işləyir. Baza əlçatan deyilsə tətbiq yenə də qalxır (xəta loglanır),
beləliklə konfiqurasiyanı Swagger üzərindən yoxlaya bilərsiniz.

### Migration-ları əl ilə tətbiq etmək

```bash
dotnet ef database update -p src/MimeduAz.Infrastructure -s src/MimeduAz.Api
```

### Yeni migration əlavə etmək

```bash
dotnet ef migrations add <Ad> -p src/MimeduAz.Infrastructure -s src/MimeduAz.Api -o Persistence/Migrations
```

### Testlər

```bash
dotnet test
```

---

## Seed hesabları (yalnız Development)

| E-poçt | Şifrə | Rol |
|---|---|---|
| `admin@mimedu.az` | `Admin123!` | Admin |
| `nigar@mimedu.az` | `Teacher123!` | Teacher (2 resurs müəllifi) |
| `elvin@mimedu.az` | `Teacher123!` | Teacher |
| `aysel@mimedu.az` | `Teacher123!` | Teacher |
| `reshad@mimedu.az` | `Teacher123!` | Teacher |
| `leyla@mimedu.az` | `Teacher123!` | Teacher |
| `kamran@mimedu.az` | `Teacher123!` | Teacher |

Test sertifikat kodu: **`MIM-2026-4417`**
(`GET /api/v1/certificates/verify/MIM-2026-4417`)

`admin@mimedu.az` ilə qeydiyyatdan keçən istifadəçi avtomatik Admin rolu alır.

---

## Layihə strukturu

```
MimeduAz.sln
├── src/
│   ├── MimeduAz.Domain/          # Entity-lər, enum-lar, sabitlər — heç bir asılılıq
│   ├── MimeduAz.Contracts/       # Request/response DTO-ları (frontend ilə paylaşılan müqavilə)
│   ├── MimeduAz.Application/     # Servis interfeysləri + biznes məntiq, validatorlar, options
│   ├── MimeduAz.Infrastructure/  # EF Core DbContext, konfiqurasiyalar, migrations, JWT, fayl saxlama
│   └── MimeduAz.Api/             # Controller-lər, middleware, Swagger, Program.cs
├── tests/
│   └── MimeduAz.Tests/           # Biznes məntiq üzrə unit testlər (38 test)
├── scripts/smoke-test.mjs        # Real API üzərində uçdan-uca yoxlama (92 assertion)
├── docker-compose.yml            # Local PostgreSQL
└── requests.http                 # Manual API testləri
```

Asılılıq istiqaməti: `Domain → Contracts → Application → Infrastructure → Api`.
Controller-lər heç vaxt `DbContext`-ə birbaşa müraciət etmir — yalnız servis qatından keçir.
Application qatı bazaya `IApplicationDbContext` abstraksiyası ilə çıxır.

---

## Konfiqurasiya

`appsettings.json` yalnız placeholder saxlayır; real dəyərlər
**user-secrets** və ya **environment variable** ilə verilməlidir.

```bash
cd src/MimeduAz.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=mimedu;Username=...;Password=..."
dotnet user-secrets set "Jwt:SecretKey" "<ən azı 32 simvol>"
```

| Açar | Təyinat |
|---|---|
| `ConnectionStrings:DefaultConnection` | PostgreSQL bağlantısı |
| `Jwt:Issuer` / `Jwt:Audience` | Token issuer/audience |
| `Jwt:SecretKey` | HMAC imza açarı (**ən azı 32 simvol**, əks halda tətbiq qalxmır) |
| `Jwt:AccessTokenExpiryMinutes` | Access token ömrü (default 15) |
| `Jwt:RefreshTokenExpiryDays` | Refresh token ömrü (default 7) |
| `Commission:ResourcePercent` | Platforma komissiyası (`0.20` = 20%) |
| `FileStorage:RootPath` | `wwwroot`-a nisbətən yükləmə qovluğu |
| `FileStorage:MaxFileSizeBytes` | Maksimum fayl ölçüsü (default 25 MB) |
| `FileStorage:AllowedExtensions` | İcazə verilən formatlar (`.pdf`, `.docx`, `.pptx`) |
| `Cors:AllowedOrigins` | Frontend origin-ləri (dev: `http://localhost:5173`) |

---

## Biznes qaydaları

1. **Komissiya** — ödənişli resurs satışından **20% platformaya**, **80% müəllifə**.
   Faiz `Commission:ResourcePercent` ilə konfiqurasiya olunur; hər sifariş sətrində
   `CommissionAmount` və `AuthorPayoutAmount` snapshot kimi saxlanılır.
   Təlimlər platformanın öz məhsuludur — müəllif payı yoxdur.
2. **Moderasiya** — hər yeni resurs `Pending` statusunda başlayır və yalnız Admin
   `Approved` etdikdən sonra Resurs Bankında ictimai görünür. Müəllif və Admin
   təsdiqlənməmiş resursu görə bilir.
3. **Kodlar** — sifariş `MIM-XXXX`, sertifikat `MIM-YYYY-XXXX` formatında, unikallıq
   DB yoxlanışı ilə təmin olunur (təkrar zamanı 6 rəqəmli suffiksə keçir).
4. **Quiz** — keçid balı default 70%, hər quiz üçün ayrıca təyin oluna bilər.
   Keçid balı toplananda **avtomatik sertifikat** yaradılır (eyni cəhd üçün idempotent).
   `correctOptionIndex` heç vaxt API cavabına düşmür.
5. **Fayl yükləmə** — PDF/DOCX/PPTX, maks. 25 MB, `IFileStorageService` abstraksiyası
   arxasında. Local implementasiya `wwwroot/uploads/resources/{guid}-{ad}` yolunda saxlayır
   və path traversal-a qarşı yoxlama aparır.
6. **Ödəniş** — tam demo. Checkout xarici provayderə müraciət etmir, kart sahələri
   qəbul edilmir. Səbət sifarişə çevrilir, təlim sətirləri üçün `Enrollment` yaradılır,
   səbət boşaldılır — hamısı tək `SaveChanges` çağırışında atomik icra olunur.

---

## Autentifikasiya

- ASP.NET Core Identity (`IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`).
- Rollar: `Teacher`, `Admin` (seed zamanı yaradılır).
- JWT access token (qısa ömürlü) + DB-də saxlanılan, **rotasiya olunan** refresh token.
  `POST /auth/refresh` köhnə token-i ləğv edib yenisini verir.
- Şifrələr yalnız Identity-nin hash mexanizmi ilə saxlanılır.
- Swagger UI-də `Authorize` düyməsi ilə Bearer token daxil edilə bilər.

---

## API xülasəsi

Bütün endpoint-lər `/api/v1/` prefiksi ilə.

### Auth
| Metod | Yol | İcazə |
|---|---|---|
| POST | `/auth/register` | Publik |
| POST | `/auth/login` | Publik |
| POST | `/auth/refresh` | Publik |
| POST | `/auth/logout` | Publik |
| GET | `/auth/me` | Authorize |

### Resurslar
| Metod | Yol | İcazə |
|---|---|---|
| GET | `/resources` | Publik (filtr: `subject`, `grade`, `type`, `isPaid`, `search`, `page`, `pageSize`) |
| GET | `/resources/{id}` | Publik |
| POST | `/resources` | Authorize (multipart/form-data) |
| POST | `/resources/{id}/download` | Publik (ödənişli üçün satın alma tələb olunur) |
| GET | `/resources/mine` | Authorize |
| GET | `/resources/author/{userId}` | Publik |
| GET | `/resources/{resourceId}/quiz` | Publik |
| POST | `/resources/{resourceId}/quiz` | Authorize (müəllif və ya Admin) |

### Təlimlər
| Metod | Yol | İcazə |
|---|---|---|
| GET | `/trainings` | Publik |
| GET | `/trainings/{id}` | Publik |
| POST | `/trainings` | Admin |
| GET | `/trainings/mine` | Authorize |

### Səbət və sifariş
| Metod | Yol | İcazə |
|---|---|---|
| GET | `/cart` | Authorize |
| POST | `/cart/items` | Authorize |
| DELETE | `/cart/items/{id}` | Authorize |
| POST | `/orders/checkout` | Authorize |
| GET | `/orders/mine` | Authorize |

### İmtahan
| Metod | Yol | İcazə |
|---|---|---|
| GET | `/quiz/{quizId}/questions` | Authorize |
| POST | `/quiz/{quizId}/submit` | Authorize |

### Sertifikatlar
| Metod | Yol | İcazə |
|---|---|---|
| GET | `/certificates/mine` | Authorize |
| GET | `/certificates/verify/{code}` | **Publik** |
| POST | `/certificates/issue` | Admin |

### Admin
| Metod | Yol |
|---|---|
| GET | `/admin/resources/pending` |
| POST | `/admin/resources/{id}/approve` |
| POST | `/admin/resources/{id}/reject` |
| GET | `/admin/users` |
| GET | `/admin/sales` |
| GET | `/admin/orders` |

### Blog
| Metod | Yol | İcazə |
|---|---|---|
| GET | `/blog` | Publik |
| GET | `/blog/{id}` | Publik |
| POST | `/blog` | Admin |
| PUT | `/blog/{id}` | Admin |
| DELETE | `/blog/{id}` | Admin |

---

## Xəta formatı

Bütün xətalar vahid formatda qayıdır:

```json
{
  "statusCode": 400,
  "message": "Göndərilən məlumatlar düzgün deyil.",
  "errors": {
    "password": ["Şifrə ən azı 8 simvol olmalıdır."]
  },
  "traceId": "0HN7..."
}
```

`errors` yalnız validasiya xətalarında olur. Production-da 500 xətalarının
detalları cavabda göstərilmir, yalnız loglanır.

---

## Spesifikasiyadan fərqlər və texniki qərarlar

| Mövzu | Qərar | Səbəb |
|---|---|---|
| `QuizQuestion.Options`, `BlogPost.Body` | `jsonb` əvəzinə **`text[]`** | Npgsql `List<string>`-i birbaşa `jsonb` parametrinə yaza bilmir (`InvalidCastException`); `text[]` doğma massivdir və sorğulana bilir. Spesifikasiya hər iki variantı icazə verirdi. |
| Səbət | Yalnız daxil olmuş istifadəçi üçün | Spesifikasiya qonaq səbətini opsional saxlamışdı. `Cart.UserId` nullable qalıb — gələcəkdə guest səbəti üçün struktur hazırdır. |
| Blog CRUD | Admin üçün `POST`/`PUT`/`DELETE` əlavə edildi | Spesifikasiyanın 1-ci bölməsi "sadə CMS-style CRUD" tələb edirdi; 4.8-də yalnız GET sadalanmışdı. |
| Postgres host portu | 5432 əvəzinə **5434** | Maşındakı digər layihənin konteyneri 5432-ni tutub. |
| Sifariş sətrində komissiya | `CommissionAmount` + `AuthorPayoutAmount` sətir səviyyəsində snapshot kimi saxlanılır | Komissiya faizi sonradan dəyişsə də keçmiş sifarişlərin hesabatı sabit qalır. |
| `Resource.RejectionReason` | Əlavə sahə | Moderasiya rəddinin səbəbini müəllifə göstərmək üçün. |
| Fayl yükləmə | Fayl `Pending` mərhələsində də diskə yazılır | Admin təsdiqləməzdən əvvəl materialı yoxlaya bilsin deyə. |

---

## Növbəti mərhələ

Frontend (React + Vite + TypeScript + Tailwind) — mövcud UI prototipinin bu API-yə
qoşulması. Backend təsdiqləndikdən sonra başlanacaq.
