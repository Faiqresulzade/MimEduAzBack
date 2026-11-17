/**
 * MIMEDU.AZ — ödəniş və bank uyğunluğu (AzeriCard) üzrə uçdan-uca test.
 *
 * API işləyərkən (seed data ilə, Payment:Enabled=false — standart):
 *   node scripts/payment-test.mjs
 *
 * Yoxlanılır: ödənişli alış ödənişsiz giriş VERMİR, şərtlərə razılıq məcburidir,
 * müəllif ödənişi kart nömrəsi qəbul etmir, callback imzasız nəticəni rədd edir,
 * təhlükəsizlik header-ləri və real statistika.
 */

const BASE = process.env.MIMEDU_API ?? 'http://localhost:5297/api/v1';

let passed = 0;
let failed = 0;

function check(name, condition, detail = '') {
  if (condition) {
    passed++;
    console.log(`  PASS  ${name}`);
  } else {
    failed++;
    console.log(`  FAIL  ${name}${detail ? ` — ${detail}` : ''}`);
  }
}

function section(title) {
  console.log(`\n${title}`);
}

async function call(method, path, { token, body, form, redirect = 'follow' } = {}) {
  const headers = {};
  if (token) headers.Authorization = `Bearer ${token}`;
  if (body) headers['Content-Type'] = 'application/json';
  if (form) headers['Content-Type'] = 'application/x-www-form-urlencoded';

  const res = await fetch(`${BASE}${path}`, {
    method,
    headers,
    redirect,
    body: form ? new URLSearchParams(form).toString() : body ? JSON.stringify(body) : undefined,
  });

  const text = await res.text();
  let parsed;
  try {
    parsed = text ? JSON.parse(text) : undefined;
  } catch {
    parsed = undefined;
  }
  return { status: res.status, body: parsed, text, headers: res.headers };
}

async function login(email, password) {
  const res = await call('POST', '/auth/login', { body: { email, password } });
  return res.body?.accessToken;
}

async function main() {
  section('Konfiqurasiya və statistika');
  const config = await call('GET', '/payments/config');
  check('kartla ödəniş söndürülüb (terminal yoxdur)', config.status === 200 && config.body.enabled === false, config.text);
  check('valyuta AZN', config.body?.currency === 'AZN');

  const stats = await call('GET', '/stats');
  check('statistika real rəqəmlərdir (baza rəqəm yoxdur)',
    stats.status === 200 && stats.body.resourceCount < 1000 && stats.body.teacherCount < 2400, stats.text);

  section('Təhlükəsizlik header-ləri');
  check('X-Content-Type-Options: nosniff', stats.headers.get('x-content-type-options') === 'nosniff');
  check('X-Frame-Options: DENY', stats.headers.get('x-frame-options') === 'DENY');
  check('Content-Security-Policy /api-də', (stats.headers.get('content-security-policy') ?? '').includes("frame-ancestors 'none'"));
  check('Referrer-Policy', stats.headers.get('referrer-policy') === 'strict-origin-when-cross-origin');

  section('Qeydiyyatda şərtlərə razılıq');
  const email = `odenis-${Date.now()}@test.az`;
  const base = { fullName: 'Ödəniş Test', email, password: 'Sinaq12345', accountType: 'Teacher' };
  const noTerms = await call('POST', '/auth/register', { body: base });
  check('razılıqsız qeydiyyat — 400', noTerms.status === 400, noTerms.text);
  const registered = await call('POST', '/auth/register', { body: { ...base, acceptTerms: true } });
  check('razılıqla qeydiyyat — 200', registered.status === 200, registered.text);
  const token = registered.body?.accessToken;

  section('Ödənişli alış ödənişsiz giriş vermir');
  const trainings = await call('GET', '/trainings');
  const paidTraining = trainings.body?.find((t) => t.price > 0);
  check('ödənişli təlim tapıldı', Boolean(paidTraining));

  const add = await call('POST', '/cart/items', { token, body: { itemType: 'Training', itemId: paidTraining.id } });
  check('səbətə əlavə olundu', add.status === 200 || add.status === 201, add.text);

  const withoutTerms = await call('POST', '/orders/checkout', { token, body: {} });
  check('şərtlərə razılıqsız checkout — 400', withoutTerms.status === 400, withoutTerms.text);

  const checkout = await call('POST', '/orders/checkout', { token, body: { acceptTerms: true } });
  check('ödəniş söndürülüb — checkout 409', checkout.status === 409, checkout.text);
  check('mesaj: kartla ödəniş tezliklə', (checkout.body?.message ?? '').includes('tezliklə'));

  const orders = await call('GET', '/orders/mine', { token });
  check('heç bir sifariş yaradılmayıb', orders.body?.length === 0, orders.text);

  const mine = await call('GET', '/trainings/mine', { token });
  check('təlimə giriş açılmayıb', !(mine.body ?? []).some((t) => (t.training?.id ?? t.trainingId ?? t.id) === paidTraining.id), mine.text);

  const cart = await call('GET', '/cart', { token });
  check('səbət toxunulmaz qalıb', cart.body?.items?.length === 1);

  section('Bank callback-i');
  const forged = await call('POST', '/payments/azericard/callback', {
    form: { ORDER: '999999999999', AMOUNT: '120.00', ACTION: '0', RC: '00', P_SIGN: 'deadbeef' },
    redirect: 'manual',
  });
  check('saxta callback — 303 uğursuz səhifəyə', forged.status === 303
    && (forged.headers.get('location') ?? '').includes('/odenis/ugursuz'), `${forged.status} ${forged.headers.get('location')}`);

  section('Müəllif ödənişi: kart nömrəsi qəbul edilmir');
  const nigar = await login('nigar@mimedu.az', 'Teacher123!');
  const earnings = await call('GET', '/payouts/mine', { token: nigar });
  check('qazanc yalnız ödənilmiş satışlardan', earnings.status === 200 && typeof earnings.body.available === 'number', earnings.text);
  check('minimum 20 ₼', earnings.body?.minimumPayout === 20);

  const card = await call('POST', '/payouts', {
    token: nigar,
    body: { iban: '4169 7388 1234 5678', accountHolder: 'Nigar Əliyeva', fin: '5ABC12D' },
  });
  check('kart nömrəsi IBAN kimi qəbul edilmir — 400', card.status === 400, card.text);

  const lowBalance = await call('POST', '/payouts', {
    token: nigar,
    body: { iban: 'AZ21NABZ00000000137010001944', accountHolder: 'Nigar Əliyeva', fin: '5ABC12D' },
  });
  check('balans 20 ₼-dən az — 400', lowBalance.status === 400, lowBalance.text);

  const student = await login('sevinc@mimedu.az', 'Student123!');
  check('şagird payout endpoint-inə girə bilmir — 403',
    (await call('GET', '/payouts/mine', { token: student })).status === 403);

  section('Admin');
  const admin = await login('admin@mimedu.az', 'Admin123!');
  check('admin ödəniş tələbləri — 200', (await call('GET', '/admin/payouts', { token: admin })).status === 200);
  check('olmayan sifarişin qaytarılması — 404',
    (await call('POST', '/admin/orders/00000000-0000-0000-0000-000000000001/refund', { token: admin })).status === 404);
  check('müəllim admin endpoint-inə girə bilmir — 403',
    (await call('GET', '/admin/payouts', { token: nigar })).status === 403);

  console.log(`\n${passed} PASS, ${failed} FAIL`);
  process.exit(failed ? 1 : 0);
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
