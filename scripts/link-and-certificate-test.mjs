/**
 * MIMEDU.AZ — video/xarici link resursları və sertifikat sənədi yoxlanışı.
 *
 * İşə salmaq:  node scripts/link-and-certificate-test.mjs
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

async function call(method, path, { token, body } = {}) {
  const headers = {};
  if (token) headers.Authorization = `Bearer ${token}`;
  if (body !== undefined) headers['Content-Type'] = 'application/json';

  const res = await fetch(`${BASE}${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  const text = await res.text();
  let json = null;
  try {
    json = text ? JSON.parse(text) : null;
  } catch {
    /* JSON deyil */
  }
  return { status: res.status, body: json, text };
}

function section(title) {
  console.log(`\n${title}`);
}

async function main() {
  const teacher = (await call('POST', '/auth/login', {
    body: { email: 'nigar@mimedu.az', password: 'Teacher123!' },
  })).body.accessToken;

  const admin = (await call('POST', '/auth/login', {
    body: { email: 'admin@mimedu.az', password: 'Admin123!' },
  })).body.accessToken;

  const student = (await call('POST', '/auth/login', {
    body: { email: 'sevinc@mimedu.az', password: 'Student123!' },
  })).body.accessToken;

  // ------------------------------------------------------- 1. Video dərs
  section('1. Video dərs (fayl yox, link — ödənişli ola bilər)');

  const video = await call('POST', '/resources/link', {
    token: teacher,
    body: {
      name: 'Kəsrlər üzrə video dərs',
      subject: 'Riyaziyyat',
      grade: 5,
      type: 'Video',
      externalUrl: 'https://www.youtube.com/watch?v=kesrler-dersi',
      isPaid: true,
      price: 5,
    },
  });

  check('Ödənişli video yaranır (201)', video.status === 201, `status=${video.status} ${(video.text ?? '').slice(0, 140)}`);
  check('Tip Video-dur', video.body?.type === 'Video');
  check('isLinkBased = true', video.body?.isLinkBased === true);
  check('Moderasiyaya düşür (Pending)', video.body?.status === 'Pending');
  check('Ödənişlidə link cavabda GİZLİDİR', video.body?.externalUrl === null,
    `externalUrl=${video.body?.externalUrl}`);

  const freeVideo = await call('POST', '/resources/link', {
    token: teacher,
    body: {
      name: 'Həndəsə giriş — pulsuz video',
      subject: 'Riyaziyyat',
      grade: 7,
      type: 'Video',
      externalUrl: 'https://vimeo.com/123456',
    },
  });
  check('Pulsuz video yaranır', freeVideo.status === 201);
  check('Pulsuzda link açıq gəlir (embed üçün)',
    freeVideo.body?.externalUrl === 'https://vimeo.com/123456');

  // --------------------------------------------------- 2. Xarici link
  section('2. Xarici resurs (yalnız pulsuz)');

  const paidExternal = await call('POST', '/resources/link', {
    token: teacher,
    body: {
      name: 'Wordwall oyunu',
      subject: 'Riyaziyyat',
      grade: 5,
      type: 'ExternalLink',
      externalUrl: 'https://wordwall.net/az/resource/1',
      isPaid: true,
      price: 3,
    },
  });
  check('Ödənişli xarici link RƏDD olunur (400)', paidExternal.status === 400,
    `status=${paidExternal.status}`);

  const external = await call('POST', '/resources/link', {
    token: teacher,
    body: {
      name: 'Wordwall interaktiv oyun',
      subject: 'Riyaziyyat',
      grade: 5,
      type: 'ExternalLink',
      externalUrl: 'https://wordwall.net/az/resource/1',
    },
  });
  check('Pulsuz xarici link yaranır (201)', external.status === 201, `status=${external.status}`);
  check('Link cavabda açıqdır',
    external.body?.externalUrl === 'https://wordwall.net/az/resource/1');

  // ------------------------------------------------------ 3. Validasiya
  section('3. Validasiya və icazələr');

  const badUrl = await call('POST', '/resources/link', {
    token: teacher,
    body: { name: 'X', subject: 'Riyaziyyat', grade: 5, type: 'Video', externalUrl: 'bu-link-deyil' },
  });
  check('Səhv URL 400 verir', badUrl.status === 400);

  const wrongType = await call('POST', '/resources/link', {
    token: teacher,
    body: { name: 'X', subject: 'Riyaziyyat', grade: 5, type: 'WorkSheet', externalUrl: 'https://a.az' },
  });
  check('Fayl tipi bu endpoint-də rədd olunur (400)', wrongType.status === 400);

  const studentAttempt = await call('POST', '/resources/link', {
    token: student,
    body: { name: 'X', subject: 'Kimya', grade: 9, type: 'Video', externalUrl: 'https://a.az' },
  });
  check('Şagird link resurs yarada bilmir (403)', studentAttempt.status === 403);

  // --------------------------------------------- 4. Təsdiq və açılma
  section('4. Moderasiya və linkin açılması');

  await call('POST', `/admin/resources/${external.body.id}/approve`, { token: admin });
  const openExternal = await call('POST', `/resources/${external.body.id}/download`);
  check('Pulsuz link token olmadan açılır', openExternal.status === 200);
  check('isExternal = true (yeni tabda açılmalı)', openExternal.body?.isExternal === true);
  check('Xarici ünvan qayıdır',
    openExternal.body?.downloadUrl === 'https://wordwall.net/az/resource/1');
  check('Açılış sayğacı artır', openExternal.body?.downloads === 1);

  await call('POST', `/admin/resources/${video.body.id}/approve`, { token: admin });
  const lockedVideo = await call('POST', `/resources/${video.body.id}/download`, { token: student });
  check('Ödənişli video satın almadan açılmır (403)', lockedVideo.status === 403,
    `status=${lockedVideo.status}`);

  const detailPaid = await call('GET', `/resources/${video.body.id}`);
  check('Detal cavabında da link gizlidir', detailPaid.body?.externalUrl === null);

  // Satın alıb yoxlayırıq
  await call('POST', '/cart/items', {
    token: student,
    body: { itemType: 'Resource', itemId: video.body.id },
  });
  await call('POST', '/orders/checkout', { token: student, body: {} });

  const boughtVideo = await call('POST', `/resources/${video.body.id}/download`, { token: student });
  check('Satın alandan sonra video linki açılır', boughtVideo.status === 200,
    `status=${boughtVideo.status}`);
  check('Düzgün YouTube linki qayıdır',
    boughtVideo.body?.downloadUrl === 'https://www.youtube.com/watch?v=kesrler-dersi');

  const listed = await call('GET', '/resources?pageSize=100');
  check('Link resurslar siyahıda isLinkBased ilə gəlir',
    listed.body?.items?.some((r) => r.id === external.body.id && r.isLinkBased === true));

  // ------------------------------------------ 5. Sertifikat sənədi
  section('5. Sertifikat sənədi (A4 PNG / PDF)');

  const png = await fetch(`${BASE}/certificates/MIM-2026-4417/download`);
  const pngBuffer = Buffer.from(await png.arrayBuffer());
  check('PNG endirilir (token lazım deyil)', png.status === 200, `status=${png.status}`);
  check('Content-Type: image/png', png.headers.get('content-type') === 'image/png');
  check('PNG imzası düzgündür',
    pngBuffer[0] === 0x89 && pngBuffer.toString('ascii', 1, 4) === 'PNG');
  check('Fayl adı Content-Disposition-dadır',
    (png.headers.get('content-disposition') ?? '').includes('MIM-2026-4417'));
  check('PNG ölçüsü realdır (>50 KB)', pngBuffer.length > 50_000, `${pngBuffer.length} bayt`);

  const pdf = await fetch(`${BASE}/certificates/MIM-2026-4417/download?format=pdf`);
  const pdfBuffer = Buffer.from(await pdf.arrayBuffer());
  check('PDF endirilir', pdf.status === 200);
  check('Content-Type: application/pdf', pdf.headers.get('content-type') === 'application/pdf');
  check('PDF imzası düzgündür', pdfBuffer.toString('ascii', 0, 5) === '%PDF-');

  const lower = await fetch(`${BASE}/certificates/mim-2026-4417/download`);
  check('Kod böyük/kiçik hərfə həssas deyil', lower.status === 200);

  const missing = await fetch(`${BASE}/certificates/MIM-2026-0000/download`);
  check('Mövcud olmayan kod 404 verir', missing.status === 404);

  console.log(`\n${'='.repeat(50)}`);
  console.log(`NƏTİCƏ:  ${passed} keçdi, ${failed} uğursuz`);
  console.log('='.repeat(50));
  process.exit(failed === 0 ? 0 : 1);
}

main().catch((err) => {
  console.error('Test icra oluna bilmədi:', err);
  process.exit(1);
});
