# Dəyişiklik siyahısı — AzeriCard ödənişi və bank uyğunluğu

Mənbə: `mimedu-azericard-uygunluq-hesabati.md` (17.11.2026) və AzeriCard developer sənədi
(developer.azericard.com, bölmə 2, 4, 8, 9).

## 1. Qəbul edilmiş qərarlar

| Sual | Qərar |
|---|---|
| İnteqrasiya | **Hosted redirect** (TRTYPE=1). Kart məlumatı yalnız AzeriCard səhifəsində daxil edilir. |
| Terminal qoşulana qədər | `Payment:Enabled=false` — ödənişli checkout **409**, pulsuz məhsullar işləyir. |
| Satıcı | Fərdi sahibkar Vəfa Kərimli Zeyqəm, VÖEN 4002166852 (`Seller` bölməsi + frontend `src/data/legal.ts`). |
| Geri qaytarma | Yalnız xəta halında: texniki xəta, ikiqat ödəniş, təsvirə uyğunsuzluq, canlı təlimin ləğvi. |
| Müəllif ödənişi | **IBAN + ad + FİN**, bank köçürməsi. Kart nömrəsi toplanmır (əvvəlki forma heç yerə yazmırdı). |
| Marketplace | Hamısı satılır; şərtlərdə platforma satıcı, müəllim lisenziya verəndir. **ABB-yə açıq bildirilməlidir.** |
| Video təlimə giriş | Ömürlük (platforma fəaliyyət göstərdikcə). |
| Canlı təlim linki | Yalnız saytda ("Təlimlərim"), e-poçtla yox. |
| ƏDV | Neytral mətn — mühasiblə dəqiqləşdirilməlidir. |
| Refresh token | localStorage-da qalır (cookie üçün `api.mimedu.az` domeni lazımdır) — sonraya. |

## 2. Ödəniş axını

1. `POST /orders/checkout { acceptTerms, clientInfo }` → pulsuz səbət dərhal **Paid**; ödənişli —
   **Pending** sifariş + imzalı form (`payment.url`, `payment.fields`). Səbət ödənişə qədər qalır.
2. Frontend formu AzeriCard-a POST edir.
3. Bank nəticəni `POST /payments/azericard/callback` (BACKREF) ünvanına göndərir:
   - imza (AMOUNT, TERMINAL, APPROVAL, RRN, INT_REF) AzeriCard public key ilə yoxlanır;
   - **sənəddəki boşluq:** imza ORDER və ACTION-u əhatə etmir → əlavə olaraq məbləğ, terminal,
     valyuta yoxlanır, bir INT_REF yalnız bir sifarişi ödəyə bilər (unikal indeks);
   - təsdiqdən sonra **TRTYPE=21** (tamamlama) göndərilir, giriş yalnız onun uğurundan sonra açılır;
   - istifadəçi `/odenis/ugurlu` və ya `/odenis/ugursuz`-a yönləndirilir (status backend-dən oxunur).
4. Fon işi (`PaymentStatusWorker`, 10 dəq.): alınmamış tamamlamanı təkrarlayır, callback gəlməyən
   sifarişi **TRTYPE=90** ilə soruşur, 24 saatdan köhnə ödənilməmiş sifarişi ləğv edir.
5. Admin → Satış → "Qaytar": **TRTYPE=22**, sifariş Refunded, giriş bağlanır, müəllif payı çıxılır.
6. Bütün mübadilələr `payment_logs` cədvəlində (imza və nonce olmadan).

## 3. Qurulma (terminal alınandan sonra)

```bash
# Açar cütü (yalnız public key AzeriCard-a göndərilir)
openssl genrsa -out mimedu_private_key.pem 2048
openssl rsa -in mimedu_private_key.pem -pubout -out mimedu_public_key.pem
```

Render env (private key heç vaxt git-ə düşmür):

| Dəyişən | Dəyər |
|---|---|
| `AzeriCard__Terminal` | bankın verdiyi 8 simvol |
| `AzeriCard__PrivateKeyPem` | `mimedu_private_key.pem` məzmunu (`\n` ilə tək sətir də olar) və ya Secret File + `AzeriCard__PrivateKeyPath` |
| `AzeriCard__GatewayPublicKeyPem` | AzeriCard-ın verdiyi public key |
| `AzeriCard__GatewayUrl` | prod ünvanı (bank verir); test: `https://testmpi.3dsecure.az/cgi-bin/cgi_link` |
| `Payment__Enabled` | `true` — yalnız sandbox testləri keçəndən sonra |

Frontend `public/_headers` CSP-də `form-action`-a prod gateway domeni əlavə olunmalıdır (indi `*.3dsecure.az`).

## 4. Sandbox-da yoxlanmalı (sənəddə ziddiyyətlər var)

- [ ] Satış imzasının sahə ardıcıllığı test terminalında qəbul olunur (sənəd: "terminal üzrə dəqiqləşir").
- [ ] Boş sahə `-` kimi uzunluqsuz yazılır (callback-də APPROVAL boş gələ bilər).
- [ ] RSA padding PKCS#1 v1.5.
- [ ] ORDER uzunluğu: 16 rəqəm istifadə olunur (sənəd 6–20 və 6–32 deyir).
- [ ] BACKREF brauzer POST-udur (və ya server-server) — endpoint hər ikisini qəbul edir.
- [ ] TRTYPE=21 tamamlama satış (TRTYPE=1) üçün tələb olunurmu.
- [ ] TRTYPE=90 cavabının imza sahələri callback ilə eynidirmi.
- [ ] C8 ssenariləri: uğurlu, rədd, 3DS uğursuz, səhifə bağlandı, ikiqat callback, məbləği dəyişdirilmiş callback, qaytarma.

Test kartları (sənəd, bölmə 4.1, SMS kodu `1111`): 4127208104942601 05/31 796 · 5167513332880780 05/31 609.

## 5. Digər dəyişikliklər

- Kart forması (`PaymentForm`, "Demo rejim") silindi; səbətdə xülasə + 2 razılıq checkbox-u.
- Qeydiyyatda (e-poçt və Google) şərtlərə razılıq məcburidir; vaxt və versiya `users`-də saxlanır.
- Yeni səhifələr: `/istifade-sertleri`, `/geri-qaytarma`, `/xidmetin-teqdimi`, `/odenis-tehlukesizliyi`,
  `/elaqe`, `/odenis/ugurlu`, `/odenis/ugursuz`; footer-də "Hüquqi" sütunu, satıcı rekvizitləri.
- Məxfilik siyasəti: nəzarətçi, ödəniş məlumatları, ABB/AzeriCard, IBAN/FİN, localStorage, saxlanma.
- Uydurma rəylər və şişirdilmiş statistika (1170 resurs, 2400 müəllim) silindi → `GET /api/v1/stats`.
- Prod təmizlik: 3 test resursu silindi, "testssa" sınağı Rejected (3 sertifikat qorunub).
- Təhlükəsizlik: backend HSTS + nosniff/X-Frame-Options/Referrer-Policy/CSP; frontend `public/_headers`.
- Migration: `AddPaymentsAndPayouts` (yalnız əlavə: sütunlar, `payment_logs`, `payout_requests`).

## 6. Kodla həll olunmayan bəndlər (səndə)

- ABB-yə marketplace modelini yazılı bildirmək (A10), ABB-dən öz checklist-ini istəmək.
- Cloudflare: Always Use HTTPS, TLS ≥ 1.2, Full (strict), `www` DNS + 301 (B15, B16); SSL Labs yoxlaması.
- Rəsmi Visa/Mastercard loqo faylları (bankdan) — indi mətn nişanları var.
- Domen e-poçtu (`info@mimedu.az`, Cloudflare Email Routing) — indi satıcının Gmail-i.
- İş saatları (`SUPPORT_HOURS`) mənim fərziyyəmdir — düzəldin.
- Mobil tətbiq: Google Play qaydasına görə tətbiq daxilində rəqəmsal məzmunun kartla satışı problem yarada bilər (C7).
- Prod-dakı 5 köhnə "demo" sifariş Paid qalır (MIM-9995 daxil) — real ödəniş olmayıb.
- `admin-and-purchases-test.mjs`, `exam-test.mjs`, `link-and-certificate-test.mjs` ödənişli alışı demo checkout ilə edirdi — indi alış addımlarında uğursuz olur.
