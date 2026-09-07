# MIMEDU.AZ — Backend

Azərbaycan müəllimləri üçün rəqəmsal təhsil platformasının backend API-si.
ASP.NET Core 8 Web API + PostgreSQL (Supabase) + EF Core (Code-First), JWT autentifikasiya.

> **Ödəniş barədə:** checkout tam **demo/mock** rejimdədir. Backend heç bir kart məlumatı
> qəbul etmir və saxlamır; sifariş birbaşa `Paid` statusunda yaradılır.

---

## Tələblər

| Alət | Versiya |
|---|---|
| .NET SDK | 8.0+ |
| PostgreSQL | 14+ (yerli inkişaf üçün Docker kifayətdir) |
| `dotnet-ef` | 8.0.8 (`dotnet tool update --global dotnet-ef --version 8.0.8`) |

---

## Sürətli başlanğıc

```bash
# 1. Local PostgreSQL-i qaldır (Docker ilə) — host portu 5434
docker compose up -d

# 2. API-ni işə sal (migration + seed avtomatik tətbiq olunur)
dotnet run --project src/MimeduAz.Api

# 3. Swagger: http://localhost:5297/swagger
```

> Konteyner **5434** host portuna bağlanır ki, maşında artıq işləyən başqa
> PostgreSQL instansı ilə toqquşmasın. Dəyişmək istəsəniz `docker-compose.yml`
> və `appsettings.Development.json`-dakı portu birlikdə yeniləyin.

### Uçdan-uca smoke test

API işlədiyi halda bütün axını (giriş → səbət → demo checkout → komissiya →
imtahan → sertifikat → moderasiya → fayl yükləmə) yoxlayır:

```bash
node scripts/smoke-test.mjs
```

> Skript təzə seed data gözləyir. Təkrar icradan əvvəl bazanı sıfırlayın (local Docker):
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
├── scripts/smoke-test.mjs        # Real API üzərində uçdan-uca yoxlama (100 assertion)
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
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=HOST;Port=5432;Database=DB;Username=USER;Password=PASS;SSL Mode=Require;Trust Server Certificate=true"
dotnet user-secrets set "Jwt:SecretKey" "<ən azı 32 simvol>"
```

| Açar | Təyinat |
|---|---|
| `ConnectionStrings:DefaultConnection` | PostgreSQL bağlantısı (Npgsql formatı) |
| `Jwt:Issuer` / `Jwt:Audience` | Token issuer/audience |
| `Jwt:SecretKey` | HMAC imza açarı (**ən azı 32 simvol**, əks halda tətbiq qalxmır) |
| `Jwt:AccessTokenExpiryMinutes` | Access token ömrü (default 15) |
| `Jwt:RefreshTokenExpiryDays` | Refresh token ömrü (default 7) |
| `Commission:ResourcePercent` | Platforma komissiyası (`0.20` = 20%) |
| `FileStorage:RootPath` | `wwwroot`-a nisbətən yükləmə qovluğu |
| `FileStorage:MaxFileSizeBytes` | Maksimum fayl ölçüsü (default 25 MB) |
| `FileStorage:AllowedExtensions` | İcazə verilən formatlar (`.pdf`, `.docx`, `.pptx`) |
| `Cors:AllowedOrigins` | Frontend origin-ləri (dev: `http://localhost:5173`) |

### CORS problemi yaşayırsınızsa

Brauzer CORS xətasında **səbəbi göstərmir** — həmişə ümumi "CORS error" yazır.
Səbəb server loglarındadır:

1. Tətbiq qalxarkən icazəli origin-lər loglanır:
   `CORS icazə verilən origin-lər: https://mimedu.az, ...`
2. İcazəsiz origin gələndə xəbərdarlıq yazılır:
   `CORS: icazə verilməyən origin rədd edildi -> https://... (yol: /api/v1/...)`

Origin **hərfi** müqayisə olunur — sxem, domen və port tam üst-üstə düşməlidir.
`https://mimedu.az` ilə `https://www.mimedu.az` **fərqli origin-lərdir**, hər ikisi
işlənəcəksə hər ikisi siyahıda olmalıdır. Sondakı `/` və artıq boşluqlar
konfiqurasiya oxunarkən avtomatik təmizlənir.

Origin-lər iki formatda verilə bilər:
```
Cors__AllowedOrigins=https://mimedu.az,https://www.mimedu.az    # vergüllə (rahat)
Cors__AllowedOrigins__0=https://mimedu.az                        # indeksli
Cors__AllowedOrigins__1=https://www.mimedu.az
```

---

## Production bazası (Supabase PostgreSQL)

Bağlantı sətri **yalnız user-secrets** və ya environment variable ilə verilir,
heç bir `appsettings*.json` faylına yazılmır.

```bash
cd src/MimeduAz.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=<region>.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<proje-ref>;Password=<parol>;SSL Mode=Require;Trust Server Certificate=true;Pooling=true;Maximum Pool Size=10;Connection Idle Lifetime=60;Keepalive=30"
```

### ⚠️ Port 5432 (session pooler) istifadə edin, 6543 yox

Supabase iki pooler portu verir və bu layihə üçün **yalnız 5432 uyğundur**:

| Port | Rejim | Bu layihə üçün |
|---|---|---|
| **5432** | Session pooler | ✅ **Düzgün seçim** |
| 6543 | Transaction pooler | ❌ İşləmir |

**Səbəb:** transaction pooler hər tranzaksiyadan sonra server tərəfdəki bağlantını
buraxır, amma Npgsql-in öz client-side pool-u həmin bağlantını "canlı" sayıb
təkrar istifadə etməyə çalışır → sorğu cavabsız qalır və ~30 saniyədən sonra
`Exception while reading from stream / Timeout during reading attempt` xətası verir.
Bu, uzunömürlü server tətbiqlərində (bizim ASP.NET Core kimi) transaction pooler-in
məlum uyğunsuzluğudur — 6543 daha çox serverless/edge funksiyalar üçündür.

Ölçülmüş fərq (4 ardıcıl sorğu, eyni bağlantı sətri):

```
Port 5432 (session):     4/4 uğurlu, təkrar istifadə ~240ms
Port 6543 (transaction): növbələşən uğur/timeout — hər pooled təkrar istifadə ilişir
```

### Migration-ları uzaq bazaya tətbiq etmək

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet ef database update -p src/MimeduAz.Infrastructure -s src/MimeduAz.Api
```
(`ASPNETCORE_ENVIRONMENT=Development` user-secrets-in yüklənməsi üçün lazımdır —
`dotnet ef` alətləri default olaraq `Production` mühitini fərz edir.)

> Supabase-in pulsuz planında layihə uzun müddət istifadə olunmayanda "yuxuya"
> keçir; ilk sorğu 2-4 saniyə çəkə bilər. Bağlantı sətrindəki `EnableRetryOnFailure`
> (kodda) və `Keepalive=30` bunu yumşaldır.

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

## HTTP audit log (request/response DB-də)

Bütün API sorğuları və cavabları `request_logs` cədvəlinə yazılır: metod, yol, status,
müddət, istifadəçi, IP, User-Agent və (maskalanmış) request/response gövdəsi.

### Həssas məlumatların qorunması

Bu, spesifikasiyanın *"heç bir şəxsi/həssas məlumat loglanmasın"* tələbi ilə
toqquşduğu üçün gövdələr yazılmazdan əvvəl `SensitiveDataRedactor`-dan keçir:

- Adında `password`, `token`, `secret`, `authorization`, `apikey`, `cardnumber`,
  `cvc`, `cvv` olan **hər JSON sahəsi** (iç-içə obyektlər və massivlər daxil) `***` ilə əvəz olunur.
- İstisna: `...ExpiresAt` / `...ExpiryMinutes` kimi vaxt sahələri oxunaqlı qalır — həssas deyil.
- **JSON olmayan gövdələr tamamilə buraxılır** — tanınmayan formatda şifrənin gizli
  qalacağına zəmanət yoxdur.
- **Fayl yükləmələri (`multipart/*`) oxunmur** — yalnız `[tip, N bayt]` qeyd olunur
  (25 MB-lıq faylı yaddaşa oxumamaq üçün).
- Binary/qeyri-JSON cavablar da gövdəsiz qeyd olunur.

Davranış 14 unit testlə (`SensitiveDataRedactorTests`) qorunur.

### Performans

Loglama sorğunu **ləngitmir**: qeydlər yaddaşdakı məhdud tutumlu növbəyə atılır,
`RequestLogWriter` arxa plan servisi onları batch şəklində bazaya yazır.
Növbə dolarsa ən köhnə qeyd atılır — log yazmaq üçün heç vaxt sorğu gözlədilmir və
baza xətası sorğunu pozmur.

### Konfiqurasiya (`appsettings.json` → `RequestLog`)

| Açar | Default | Təyinat |
|---|---|---|
| `Enabled` | `true` | Loglamanı tamamilə söndürür |
| `LogRequestBody` / `LogResponseBody` | `true` | Gövdələrin yazılması |
| `MaxBodyLength` | `4000` | Gövdə bu uzunluqdan sonra kəsilir |
| `ExcludedPathPrefixes` | `/swagger`, `/uploads`, `/health`, `/favicon.ico` | Loglanmayan yollar |
| `QueueCapacity` | `2000` | Yaddaşdakı növbənin tutumu |
| `BatchSize` / `FlushIntervalSeconds` | `50` / `5` | Bazaya yazma tezliyi |
| `RetentionDays` | `30` | Bu gündən köhnə qeydlər avtomatik silinir (`0` = silmə) |

### Loglara baxış

```
GET /api/v1/admin/logs?onlyErrors=true&pageSize=50
```
Filtrlər: `method`, `path`, `statusCode`, `userId`, `onlyErrors`, `from`, `to`,
`page`, `pageSize`. Yalnız **Admin** rolu üçün.

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
| GET | `/admin/logs` |

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
| DB provideri | **PostgreSQL (Supabase)** | Natro/Plesk MySQL-i Render-dən əlçatan deyildi: Plesk yalnız 5 sabit IP-yə icazə verir, Render-in çıxış IP-si isə 2×/24 aralıqda dəyişkəndir. Supabase IP whitelist tələb etmir. |
| Supabase pooler portu | **5432** (session), 6543 yox | Transaction pooler Npgsql-in client-side pool-u ilə uyğun gəlmir — pooled bağlantının təkrar istifadəsi timeout verir. |
| `QuizQuestion.Options`, `BlogPost.Body` | `jsonb` əvəzinə **`text[]`** | Npgsql `List<string>`-i birbaşa `jsonb` parametrinə yaza bilmir; `text[]` doğma massivdir və sorğulana bilir. |
| Səbət | Yalnız daxil olmuş istifadəçi üçün | Spesifikasiya qonaq səbətini opsional saxlamışdı. `Cart.UserId` nullable qalıb — gələcəkdə guest səbəti üçün struktur hazırdır. |
| Blog CRUD | Admin üçün `POST`/`PUT`/`DELETE` əlavə edildi | Spesifikasiyanın 1-ci bölməsi "sadə CMS-style CRUD" tələb edirdi; 4.8-də yalnız GET sadalanmışdı. |
| Local dev DB portu | 5434 (Docker) | Maşında artıq işləyən başqa PostgreSQL instansı ilə toqquşmasın deyə. |
| Sifariş sətrində komissiya | `CommissionAmount` + `AuthorPayoutAmount` sətir səviyyəsində snapshot kimi saxlanılır | Komissiya faizi sonradan dəyişsə də keçmiş sifarişlərin hesabatı sabit qalır. |
| `Resource.RejectionReason` | Əlavə sahə | Moderasiya rəddinin səbəbini müəllifə göstərmək üçün. |
| Fayl yükləmə | Fayl `Pending` mərhələsində də diskə yazılır | Admin təsdiqləməzdən əvvəl materialı yoxlaya bilsin deyə. |

---

## Render-də deploy

Layihə **.NET**-dir, Node.js yox — Render-in defolt `yarn` / `yarn start` sahələri
bura aid deyil. **Dockerfile ilə deploy edin:**

1. Render-də **New → Web Service** yaradın, bu repo-nu seçin.
2. **Language/Environment** sahəsində **Docker** seçin (Node yox). Bu seçim
   Build/Start Command sahələrini tamamilə gizlədir — Render kök qovluqdakı
   `Dockerfile`-ı avtomatik tapıb istifadə edir, əlavə heç nə yazmağa ehtiyac yoxdur.
3. **Environment Variables** bölməsində aşağıdakıları əlavə edin (`__` iki alt xətt
   ilə nested konfiqurasiya ayrılır — ASP.NET Core-un standart qaydasıdır):

   | Açar | Nümunə dəyər |
   |---|---|
   | `ConnectionStrings__DefaultConnection` | `server=<host>;port=3306;database=<db>;user=<user>;password=<parol>;` |
   | `Jwt__Issuer` | `https://<render-service-adı>.onrender.com` |
   | `Jwt__Audience` | frontend-in domeni, məs. `https://mimedu.az` |
   | `Jwt__SecretKey` | ən azı 32 simvollu təsadüfi mətn |
   | `Cors__AllowedOrigins` | frontend origin-ləri, vergüllə: `https://mimedu.az,https://www.mimedu.az` |
   | `Swagger__Enabled` | `true` — Swagger UI-ni production-da da açır (opsional, aşağıya bax) |

   `PORT` dəyişənini Render özü avtomatik verir — əlavə etməyə ehtiyac yoxdur,
   `Dockerfile`-dakı `ENTRYPOINT` onu oxuyub `--urls` ilə bağlayır.

4. `ASPNETCORE_ENVIRONMENT` təyin etməsəniz Render defolt olaraq `Production`
   göndərir — bu, düzgün davranışdır: migration avtomatik tətbiq olunur, seed
   data (demo istifadəçilər) **yaradılmır**, Swagger UI defolt olaraq **bağlıdır**.

### Swagger-i production-da açmaq

`ASPNETCORE_ENVIRONMENT=Development` etmək **tövsiyə olunmur** — bu, seed data-nı
(demo istifadəçilər) yenidən yaradar və JWT/CORS-un dev tənzimləmələrini aktivləşdirər.
Bunun əvəzinə mühiti toxunmadan yalnız Swagger-i açan ayrıca flag var:

```
Swagger__Enabled=true
```

Bunu Render-in Environment Variables-a əlavə edib yenidən deploy edin —
`https://<service-adı>.onrender.com/swagger` açılacaq. Sınaqdan sonra dəyişəni
silib deploy etsəniz, Swagger yenidən bağlanır (`Production`-da defolt davranış).

**Diqqət ediləcək məqamlar:**
- **Fayl yükləmə ephemeral disk üzərindədir.** Render-in pulsuz/standart planında
  konteynerin fayl sistemi hər yeni deploy-da sıfırlanır — `wwwroot/uploads/resources`-a
  yüklənmiş resurslar itəcək. Real istifadə üçün ya Render-in **Persistent Disk**
  add-on-unu qoşun, ya da `IFileStorageService`-in yeni bir implementasiyasını
  (S3/Azure Blob) yazın — interfeys artıq bu keçidə hazırdır.
- Supabase IP whitelist tələb etmir, ona görə Render-dən əlavə şəbəkə konfiqurasiyası lazım deyil.
- Local `docker build .` ilə image-i yükləmədən əvvəl yoxlaya bilərsiniz:
  ```bash
  docker build -t mimedu-api .
  docker run -p 8080:8080 \
    -e ConnectionStrings__DefaultConnection="..." \
    -e Jwt__SecretKey="..." -e Jwt__Issuer="..." -e Jwt__Audience="..." \
    mimedu-api
  ```

---

## Növbəti mərhələ

Frontend (React + Vite + TypeScript + Tailwind) — mövcud UI prototipinin bu API-yə
qoşulması. Backend təsdiqləndikdən sonra başlanacaq.
