/**
 * MIMEDU.AZ — KSQ cavab kartı yoxlamasının uçdan-uca testi.
 *
 * API işləyərkən (seed data ilə):
 *   node scripts/ksq-test.mjs                 # AI oxuması olmadan
 *   node scripts/ksq-test.mjs path/kart.jpg   # + real OpenRouter oxuması (OpenRouter:ApiKey lazımdır)
 *   node scripts/ksq-test.mjs kart.jpg kart25.jpg  # + Sərbəst (25+3) kartının oxunması
 *
 * Şəkil üçün gözlənilən kart: 14 qapalı sual, cavablar ABADEABCDBABCD
 * (açar ABCDEABCDEABCD — 3 və 10 səhv), ad "Əli Məmmədov", variant A, kod 1234.
 *
 * Skript yaratdığı testləri sonda silir — təkrar işə salmaq olar.
 */

import { readFile } from 'node:fs/promises';

const BASE = process.env.MIMEDU_API ?? 'http://localhost:5297/api/v1';
const imagePath = process.argv[2];

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

async function call(method, path, { token, body, form } = {}) {
  const headers = {};
  if (token) headers.Authorization = `Bearer ${token}`;
  if (body) headers['Content-Type'] = 'application/json';

  const res = await fetch(`${BASE}${path}`, {
    method,
    headers,
    body: form ?? (body ? JSON.stringify(body) : undefined),
  });

  const text = await res.text();
  let parsed;
  try {
    parsed = text ? JSON.parse(text) : undefined;
  } catch {
    parsed = undefined;
  }

  return { status: res.status, body: parsed, text };
}

async function login(email, password) {
  const res = await call('POST', '/auth/login', { body: { email, password } });
  return res.body?.accessToken;
}

const PRESETS = [
  { label: '15 sual (14+1)', closed: [4, 4, 4, 4, 4, 6, 6, 6, 6, 6, 7, 7, 8, 8], open: [20] },
  { label: '16 sual (13+3)', closed: [4, 4, 4, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6], open: [9, 9, 10] },
  { label: '16 sual (14+2)', closed: [4, 4, 4, 4, 4, 6, 6, 6, 6, 6, 7, 7, 8, 8], open: [10, 10] },
  { label: '20 sual (18+2)', closed: [2, 2, 2, 2, 3, 3, 3, 3, 5, 5, 5, 5, 5, 5, 7, 7, 8, 8], open: [10, 10] },
];

const KEY_A = [...'ABCDEABCDEABCD'];
const sum = (list) => list.reduce((t, v) => t + v, 0);
const repeat = (letters, n) => Array.from({ length: n }, (_, i) => letters[i % letters.length]);

async function main() {
  const nigar = await login('nigar@mimedu.az', 'Teacher123!');
  const elvin = await login('elvin@mimedu.az', 'Teacher123!');
  const student = await login('sevinc@mimedu.az', 'Student123!');
  check('müəllim və şagird daxil oldu', nigar && elvin && student);

  section('İcazələr');
  check('tokensiz 401', (await call('GET', '/ksq/tests')).status === 401);
  check('şagird 403', (await call('GET', '/ksq/tests', { token: student })).status === 403);

  section('Açar: hər preset ilə test, A və B açarları');
  const created = [];
  for (const preset of PRESETS) {
    const n = preset.closed.length;
    const res = await call('POST', '/ksq/tests', {
      token: nigar,
      body: {
        name: `KSQ test — ${preset.label}`,
        closedPoints: preset.closed,
        openPoints: preset.open,
        keyA: repeat(KEY_A, n),
        keyB: repeat([...'EDCBA'], n),
      },
    });
    check(`${preset.label}: yaradıldı`, res.status === 200, res.text);
    check(`${preset.label}: ölçülər düzgün`,
      res.body?.closedPoints.length === n && res.body?.keyA.length === n && res.body?.keyB.length === n);
    created.push(res.body);
  }
  const [t15] = created;

  const invalid = await call('POST', '/ksq/tests', {
    token: nigar, body: { name: '', closedPoints: [], openPoints: [], keyA: [], keyB: [] },
  });
  check('boş ad və 0 sual — 400', invalid.status === 400);

  const renamed = await call('PUT', `/ksq/tests/${t15.id}`, {
    token: nigar,
    body: { ...t15, name: 'Riyaziyyat KSQ-1' },
  });
  check('test yeniləndi', renamed.status === 200 && renamed.body.name === 'Riyaziyyat KSQ-1');

  const elvinTests = await call('GET', '/ksq/tests', { token: elvin });
  check('başqa müəllim testləri görmür', elvinTests.status === 200 && elvinTests.body.length === 0);
  check('başqa müəllim yeniləyə bilmir',
    (await call('PUT', `/ksq/tests/${t15.id}`, { token: elvin, body: { ...t15, name: 'x' } })).status === 404);

  section('Kart oxuma');
  const fakeForm = new FormData();
  fakeForm.append('image', new Blob(['not an image'], { type: 'application/pdf' }), 'x.pdf');
  fakeForm.append('closedCount', '14');
  fakeForm.append('openCount', '1');
  const badType = await call('POST', '/ksq/scan', { token: nigar, form: fakeForm });
  check('PDF qəbul olunmur — 400', badType.status === 400, badType.text);

  let scanned = null;
  if (imagePath) {
    const form = new FormData();
    form.append('image', new Blob([await readFile(imagePath)], { type: 'image/jpeg' }), 'kart.jpg');
    form.append('closedCount', '14');
    form.append('openCount', '1');
    const started = Date.now();
    const scan = await call('POST', '/ksq/scan', { token: nigar, form });
    console.log(`        (${Date.now() - started} ms) ${scan.text}`);
    check('AI kartı oxudu', scan.status === 200, scan.text);
    scanned = scan.body;
    if (scanned) {
      check('cavablar 14 element', scanned.cavablar.length === 14);
      check('cavablar düzgün oxundu', scanned.cavablar.join('') === 'ABADEABCDBABCD', scanned.cavablar.join(''));
      check('ad oxundu', /m(ə|e)mm(ə|e)dov/i.test(scanned.ad ?? ''), scanned.ad);
      check('variant A', scanned.variant === 'A', scanned.variant);
      check('kod 1234', scanned.kod === '1234', scanned.kod);
    }
  } else {
    console.log('  SKIP  real AI oxuması (şəkil yolu verilməyib)');
  }

  section('Nəticələr');
  const answers = scanned?.cavablar ?? [...'ABADEABCDBABCD'];
  const single = await call('POST', '/ksq/results', {
    token: nigar,
    body: {
      testId: t15.id, sinif: '9A', ad: scanned?.ad ?? 'Əli Məmmədov', variant: 'A', kod: '1234',
      cavablar: answers,
      aciq: [15],
    },
  });
  const expected = sum(t15.closedPoints) - t15.closedPoints[2] - t15.closedPoints[9] + 15;
  check('nəticə saxlandı', single.status === 200, single.text);
  check(`bal serverdə hesablandı (${expected} / 100)`, single.body?.bal === expected && single.body?.max === 100,
    `${single.body?.bal} / ${single.body?.max}`);
  check('səhv suallar 3, 10', single.body?.sehv.join(',') === '3,10', single.body?.sehv.join(','));

  const bulk = await call('POST', '/ksq/results/bulk', {
    token: nigar,
    body: {
      items: [
        { testId: t15.id, sinif: '9A', ad: 'Aysu', variant: 'A', kod: '', cavablar: KEY_A, aciq: [20] },
        { testId: t15.id, sinif: '9B', ad: 'Murad', variant: 'B', kod: '', cavablar: repeat([...'EDCBA'], 14), aciq: [] },
      ],
    },
  });
  check('toplu saxlama (2 nəticə)', bulk.status === 200 && bulk.body.length === 2, bulk.text);
  check('tam bal 100', bulk.body?.[0].bal === 100);

  const badBulk = await call('POST', '/ksq/results/bulk', {
    token: nigar,
    body: { items: [{ testId: t15.id, variant: 'C', cavablar: [], aciq: [] }] },
  });
  check('səhv variant — 400', badBulk.status === 400);

  const all = await call('GET', `/ksq/results?testId=${t15.id}`, { token: nigar });
  check('testə görə filtr: 3 nəticə', all.body?.items.length === 3);
  check('siniflər: 9A, 9B', all.body?.classes.join(',') === '9A,9B', all.body?.classes.join(','));

  const only9b = await call('GET', `/ksq/results?testId=${t15.id}&sinif=9B`, { token: nigar });
  check('sinfə görə filtr: 1 nəticə', only9b.body?.items.length === 1 && only9b.body.items[0].ad === 'Murad');

  check('başqa müəllim nəticə görmür',
    (await call('GET', '/ksq/results', { token: elvin })).body?.items.length === 0);
  check('başqa müəllim nəticə silə bilmir',
    (await call('DELETE', `/ksq/results/${single.body.id}`, { token: elvin })).status === 404);

  check('nəticə silindi', (await call('DELETE', `/ksq/results/${single.body.id}`, { token: nigar })).status === 204);
  const afterDelete = await call('GET', `/ksq/results?testId=${t15.id}`, { token: nigar });
  check('silinəndən sonra 2 nəticə', afterDelete.body?.items.length === 2);

  section('Sərbəst format: 25 qapalı + 3 açıq, qarışıq ballar');
  const customClosed = Array.from({ length: 25 }, (_, i) => (i % 5) + 1); // cəmi 75
  const customOpen = [5, 10, 10];
  const custom = await call('POST', '/ksq/tests', {
    token: nigar,
    body: {
      name: 'KSQ test — Sərbəst 25+3',
      closedPoints: customClosed,
      openPoints: customOpen,
      keyA: repeat([...'ABCDE'], 25),
      keyB: repeat([...'EDCBA'], 25),
    },
  });
  check('Sərbəst test yaradıldı', custom.status === 200, custom.text);
  created.push(custom.body);

  const reloaded = (await call('GET', '/ksq/tests', { token: nigar })).body?.find((t) => t.id === custom.body?.id);
  check('yenidən yükləndi: ballar eynidir',
    reloaded?.closedPoints.join() === customClosed.join() && reloaded?.openPoints.join() === customOpen.join());
  check('yenidən yükləndi: açarlar eynidir',
    reloaded?.keyA.join('') === repeat([...'ABCDE'], 25).join('') && reloaded?.keyB.join('') === repeat([...'EDCBA'], 25).join(''));
  check('heç bir preset-ə uyğun deyil (Sərbəst)',
    !PRESETS.some((p) => p.closed.join() === customClosed.join() && p.open.join() === customOpen.join()));

  const shortKey = await call('POST', '/ksq/tests', {
    token: nigar,
    body: { name: 'x', closedPoints: customClosed, openPoints: customOpen, keyA: repeat(['A'], 24), keyB: repeat(['A'], 25) },
  });
  check('açar sual sayından qısadır — 400', shortKey.status === 400);

  const tooMany = await call('POST', '/ksq/tests', {
    token: nigar,
    body: { name: 'x', closedPoints: repeat([1], 61), openPoints: [], keyA: repeat([null], 61), keyB: repeat([null], 61) },
  });
  check('61 qapalı sual — 400', tooMany.status === 400);

  // Şagird: 7 və 19 səhv, 25 boş.
  const expectedCustom = [...'ABCDEAECDEABCDEABCAEABCD'].concat([null]);
  let customAnswers = expectedCustom;
  const customImage = process.argv[3];
  if (customImage) {
    const form = new FormData();
    form.append('image', new Blob([await readFile(customImage)], { type: 'image/jpeg' }), 'kart.jpg');
    form.append('closedCount', '25');
    form.append('openCount', '3');
    const scan = await call('POST', '/ksq/scan', { token: nigar, form });
    console.log(`        ${scan.text}`);
    check('AI 25 suallıq kartı oxudu', scan.status === 200 && scan.body?.cavablar.length === 25, scan.text);
    check('AI 3 açıq sual qaytardı', scan.body?.aciqCavablar.length === 3);
    check('25 cavab düzgün oxundu',
      scan.body?.cavablar.map((c) => c ?? '-').join('') === expectedCustom.map((c) => c ?? '-').join(''),
      scan.body?.cavablar.map((c) => c ?? '-').join(''));
    customAnswers = scan.body?.cavablar ?? expectedCustom;
  } else {
    console.log('  SKIP  25 suallıq kartın AI oxuması (2-ci şəkil yolu verilməyib)');
  }

  const customResult = await call('POST', '/ksq/results', {
    token: nigar,
    body: {
      testId: custom.body.id, sinif: '10A', ad: 'Nərmin Qasımova', variant: 'A', kod: '7788',
      cavablar: customAnswers, aciq: [5, 8, 10],
    },
  });
  // 75 - (7-ci: 2) - (19-cu: 4) - (25-ci: 5) + 23 = 87
  check('Sərbəst: bal 87 / 100', customResult.body?.bal === 87 && customResult.body?.max === 100,
    `${customResult.body?.bal} / ${customResult.body?.max}`);
  check('Sərbəst: səhv suallar 7, 19, 25', customResult.body?.sehv.join(',') === '7,19,25', customResult.body?.sehv.join(','));

  section('Test silinəndə nəticələri də silinir');
  for (const test of created) {
    const res = await call('DELETE', `/ksq/tests/${test.id}`, { token: nigar });
    check(`${test.name} silindi`, res.status === 204);
  }
  const leftovers = await call('GET', '/ksq/results', { token: nigar });
  check('nəticə qalmadı', leftovers.body?.items.length === 0);

  console.log(`\n${passed} PASS, ${failed} FAIL`);
  process.exit(failed ? 1 : 0);
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
