# Dəyişiklik siyahısı — Google ilə giriş

İstifadəçilərin 84%-i Gmail işlədir; giriş cəhdlərinin ~90%-i uğursuz idi və şifrə bərpası yoxdur.
Google ilə girişdə şifrə olmur, köhnə hesablar eyni e-poçtla avtomatik açılır.

## Qərarlar

| Sual | Qərar |
|---|---|
| Yeni istifadəçinin rolu | Google-dan sonra bir dəfə **"Müəlliməm / Şagirdəm"** soruşulur ("Müəlliməm" seçili). Seçilənə qədər hesab yaradılmır. |
| Eyni e-poçtlu köhnə hesab | **Avtomatik birləşir** (Google e-poçtu təsdiqləyib). Şifrə ilə giriş də işləməyə davam edir. |
| Google ilə açılmış hesab | Şifrəsizdir; şifrə ilə girməyə çalışanda "Bu hesab Google ilə açılıb" deyilir. |
| Client Secret | Lazım deyil — yalnız ID token yoxlanılır (`Google.Apis.Auth`). |
| Google bağlantısı | Identity-nin `user_logins` cədvəlində (`LoginProvider = Google`) — migration yoxdur. |

## Endpoint-lər

| Metod | Yol | Qeyd |
|---|---|---|
| GET | `/api/v1/auth/google/config` | `{ clientId }` — boşdursa frontend düyməni göstərmir |
| POST | `/api/v1/auth/google` | `{ idToken, accountType? }` → `{ requiresAccountType, auth, email, fullName }` |

## Qurulma

1. Google Cloud Console → **APIs & Services → Credentials → Create credentials → OAuth client ID**,
   növ **Web application**. Authorized JavaScript origins: `https://mimedu.az`,
   `https://www.mimedu.az`, `http://localhost:5173`. Redirect URI lazım deyil.
2. Client ID-ni backend-ə verin:
   - Render: env `GoogleAuth__ClientId = <...>.apps.googleusercontent.com`
   - Lokal: `dotnet user-secrets set "GoogleAuth:ClientId" "<...>" --project src/MimeduAz.Api`
3. Frontend-də heç nə dəyişmir — Client ID backend-dən oxunur.

## Test

`GoogleAuthTests`: köhnə hesabla birləşmə, yeni istifadəçidə hesab növü sualı, şifrəsiz hesab,
etibarsız token / təsdiqlənməmiş e-poçt, Client ID olmayanda söndürülmə.
