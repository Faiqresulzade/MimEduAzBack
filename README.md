# MIMEDU.AZ — Backend

Azərbaycan müəllimləri üçün rəqəmsal təhsil platformasının backend API-si.
ASP.NET Core 8 Web API + MySQL/MariaDB + EF Core (Code-First), JWT autentifikasiya.

> **Ödəniş barədə:** checkout tam **demo/mock** rejimdədir. Backend heç bir kart məlumatı
> qəbul etmir və saxlamır; sifariş birbaşa `Paid` statusunda yaradılır.

---

## Tələblər

| Alət | Versiya |
|---|---|
| .NET SDK | 8.0+ |
| MySQL / MariaDB | 8.0+ / 10.6+ (yerli inkişaf üçün Docker kifayətdir) |
| `dotnet-ef` | 8.0.8 (`dotnet tool update --global dotnet-ef --version 8.0.8`) |

---

## Sürətli başlanğıc

```bash
# 1. Local MariaDB-ni qaldır (Docker ilə) — host portu 3307
docker compose up -d

# 2. API-ni işə sal (migration + seed avtomatik tətbiq olunur)
dotnet run --project src/MimeduAz.Api

# 3. Swagger: http://localhost:5297/swagger
```

> Konteyner **3307** host portuna bağlanır ki, maşında artıq işləyən başqa
> MySQL/MariaDB instansı ilə toqquşmasın. Dəyişmək istəsəniz `docker-compose.yml`
> və `appsettings.Development.json`-dakı portu birlikdə yeniləyin.

### Uçdan-uca smoke test

API işlədiyi halda bütün axını (giriş → səbət → demo checkout → komissiya →
imtahan → sertifikat → moderasiya → fayl yükləmə) yoxlayır:

```bash
node scripts/smoke-test.mjs
```

> Skript təzə seed data gözləyir. Təkrar icradan əvvəl bazanı sıfırlayın (local Docker):
> ```bash
> docker exec -it mimedu-mysql mysql -uroot -proot -e "DROP DATABASE IF EXISTS mimedu_dev; CREATE DATABASE mimedu_dev;"
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
├── docker-compose.yml            # Local MariaDB
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
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "server=HOST;port=3306;database=DB;user=USER;password=PASS;"
dotnet user-secrets set "Jwt:SecretKey" "<ən azı 32 simvol>"
```

| Açar | Təyinat |
|---|---|
| `ConnectionStrings:DefaultConnection` | MySQL/MariaDB bağlantısı (MySqlConnector formatı) |
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

## Production/staging bazası (Natro / Plesk)

Layihə hazırda Natro-nun Plesk paneli üzərindəki MySQL/MariaDB (10.6) bazasına
qoşulacaq şəkildə konfiqurasiya edilib. Bağlantı sətri **yalnız user-secrets**-də
saxlanılır, heç bir `appsettings*.json` faylına yazılmayıb.

```bash
cd src/MimeduAz.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "server=<host>;port=3306;database=<db>;user=<user>;password=<parol>;"
```

**Vacib qeyd — uzaqdan giriş:** Plesk-in `Remote MySQL Access` bölməsində
qoşulacaq maşının ictimai IP-si ağ siyahıya əlavə olunmalıdır, əks halda
`Access denied for user '...'@'<ip>'` xətası alınır. Development zamanı bu
qeydin bəzən qısa müddətdən sonra sıfırlandığı müşahidə olundu — problem
təkrarlansa, panel-də qeydin hələ də mövcud olduğunu yoxlayın.

Migration-ları uzaq bazaya tətbiq etmək:
```bash
ASPNETCORE_ENVIRONMENT=Development dotnet ef database update -p src/MimeduAz.Infrastructure -s src/MimeduAz.Api
```
(`ASPNETCORE_ENVIRONMENT=Development` user-secrets-in yüklənməsi üçün lazımdır —
`dotnet ef` alətləri default olaraq `Production` mühitini fərz edir.)

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
| DB provideri | PostgreSQL əvəzinə **MySQL/MariaDB** | Layihə real Natro/Plesk hostinqinə bağlandı, orada MySQL/MariaDB 10.6 mövcuddur. |
| `QuizQuestion.Options`, `BlogPost.Body` | `List<string>` → **`json`** sütun (dəyər çeviricisi ilə) | MySQL-də doğma massiv tipi yoxdur; `StringListJsonConverter` siyahını JSON mətnə çevirib saxlayır. |
| Səbət | Yalnız daxil olmuş istifadəçi üçün | Spesifikasiya qonaq səbətini opsional saxlamışdı. `Cart.UserId` nullable qalıb — gələcəkdə guest səbəti üçün struktur hazırdır. |
| Blog CRUD | Admin üçün `POST`/`PUT`/`DELETE` əlavə edildi | Spesifikasiyanın 1-ci bölməsi "sadə CMS-style CRUD" tələb edirdi; 4.8-də yalnız GET sadalanmışdı. |
| Local dev DB portu | 3307 (Docker) | Maşında artıq işləyən başqa MySQL/MariaDB instansı ilə toqquşmasın deyə. |
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
   | `Cors__AllowedOrigins__0` | frontend-in tam origin-i (CORS üçün) |

   `PORT` dəyişənini Render özü avtomatik verir — əlavə etməyə ehtiyac yoxdur,
   `Dockerfile`-dakı `ENTRYPOINT` onu oxuyub `--urls` ilə bağlayır.

4. `ASPNETCORE_ENVIRONMENT` təyin etməsəniz Render defolt olaraq `Production`
   göndərir — bu, düzgün davranışdır: migration avtomatik tətbiq olunur, seed
   data (demo istifadəçilər) **yaradılmır**, Swagger UI **bağlıdır**.

**Diqqət ediləcək məqamlar:**
- **Fayl yükləmə ephemeral disk üzərindədir.** Render-in pulsuz/standart planında
  konteynerin fayl sistemi hər yeni deploy-da sıfırlanır — `wwwroot/uploads/resources`-a
  yüklənmiş resurslar itəcək. Real istifadə üçün ya Render-in **Persistent Disk**
  add-on-unu qoşun, ya da `IFileStorageService`-in yeni bir implementasiyasını
  (S3/Azure Blob) yazın — interfeys artıq bu keçidə hazırdır.
- Uzaq MySQL bazasına (Plesk/Natro) Render-dən qoşulmaq üçün Render-in çıxış
  IP-ləri də (və ya `%` wildcard) Plesk-in `Remote MySQL Access` siyahısına
  əlavə olunmalıdır — əks halda `Access denied` xətası alınar.
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
