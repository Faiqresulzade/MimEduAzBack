/**
 * MIMEDU.AZ — admin icmalı, təlim CRUD və "aldığım resurslar" testi.
 *
 * İşə salmadan əvvəl təmiz baza ilə API-nin qalxdığından əmin olun:
 *   docker compose down -v && docker compose up -d
 *   dotnet ef database update --project src/MimeduAz.Infrastructure --startup-project src/MimeduAz.Api
 *   dotnet run --project src/MimeduAz.Api
 *
 * Sonra:  node scripts/admin-and-purchases-test.mjs
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

async function call(method, path, { token, body } = {}) {
  const headers = {};
  if (token) headers.Authorization = `Bearer ${token}`;
  if (body) headers['Content-Type'] = 'application/json';

  const res = await fetch(`${BASE}${path}`, {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined,
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

async function run() {
  const admin = await login('admin@mimedu.az', 'Admin123!');
  const teacher = await login('nigar@mimedu.az', 'Teacher123!');
  const student = await login('sevinc@mimedu.az', 'Student123!');

  check('Admin daxil olur', Boolean(admin));
  check('Müəllim daxil olur', Boolean(teacher));
  check('Şagird daxil olur', Boolean(student));

  // ------------------------------------------------- 1. Təlim CRUD
  section('1. Təlim CRUD (Admin)');

  const created = await call('POST', '/trainings', {
    token: admin,
    body: {
      name: 'Formativ qiymətləndirmə praktikumu',
      format: 'Online',
      description: 'Sinifdə formativ qiymətləndirmə alətləri',
      price: 40,
      durationHours: 6,
      metaLabel: '2 gün · 6 saat',
      syllabus: ['Giriş', 'Alətlər'],
      lessons: [{ title: 'Giriş dərsi', description: 'Baxış', videoUrl: 'https://vimeo.com/1' }],
    },
  });

  check('Admin təlim yaradır (201)', created.status === 201, `status=${created.status}`);
  const trainingId = created.body?.id;

  const updated = await call('PUT', `/trainings/${trainingId}`, {
    token: admin,
    body: {
      name: 'Formativ qiymətləndirmə praktikumu v2',
      format: 'Online',
      description: 'Yenilənmiş təsvir',
      price: 55,
      durationHours: 8,
      metaLabel: '3 gün · 8 saat',
      syllabus: ['Giriş', 'Alətlər', 'Praktika'],
    },
  });

  check('Admin təlimi yeniləyir (200)', updated.status === 200, `status=${updated.status}`);
  check('Ad yenilənir', updated.body?.name === 'Formativ qiymətləndirmə praktikumu v2');
  check('Qiymət yenilənir', updated.body?.price === 55);
  check('Proqram tam əvəz olunur', updated.body?.syllabus?.length === 3,
    `count=${updated.body?.syllabus?.length}`);
  check('Dərslər redaktədə itmir', updated.body?.lessonCount === 1,
    `lessonCount=${updated.body?.lessonCount}`);

  const teacherUpdate = await call('PUT', `/trainings/${trainingId}`, {
    token: teacher,
    body: {
      name: 'Oğurlanmış təlim',
      format: 'Online',
      description: 'Təsvir',
      price: 1,
      durationHours: 1,
      metaLabel: '1 saat',
    },
  });
  check('Müəllim təlimi redaktə edə bilmir (403)', teacherUpdate.status === 403,
    `status=${teacherUpdate.status}`);

  const badUpdate = await call('PUT', `/trainings/${trainingId}`, {
    token: admin,
    body: { name: '', format: 'Online', description: '', price: -5, durationHours: 0, metaLabel: '' },
  });
  check('Boş sahələr validasiyada 400 verir', badUpdate.status === 400, `status=${badUpdate.status}`);

  const missingUpdate = await call('PUT', '/trainings/99999999-9999-9999-9999-999999999999', {
    token: admin,
    body: {
      name: 'Yoxdur', format: 'Online', description: 'Yoxdur',
      price: 10, durationHours: 2, metaLabel: '2 saat',
    },
  });
  check('Mövcud olmayan təlim 404 verir', missingUpdate.status === 404, `status=${missingUpdate.status}`);

  const teacherDelete = await call('DELETE', `/trainings/${trainingId}`, { token: teacher });
  check('Müəllim təlimi silə bilmir (403)', teacherDelete.status === 403, `status=${teacherDelete.status}`);

  const deleted = await call('DELETE', `/trainings/${trainingId}`, { token: admin });
  check('Admin boş təlimi silir (204)', deleted.status === 204, `status=${deleted.status}`);

  const afterDelete = await call('GET', `/trainings/${trainingId}`);
  check('Silinmiş təlim 404 verir', afterDelete.status === 404, `status=${afterDelete.status}`);

  // -------------------------------- 2. Qeydiyyatı olan təlim silinmir
  section('2. Satılmış təlim silinmir (409)');

  const trainings = await call('GET', '/trainings');
  const seeded = trainings.body?.[0];
  check('Seed təlimləri mövcuddur', Boolean(seeded?.id));

  await call('POST', '/cart/items', {
    token: student,
    body: { itemType: 'Training', itemId: seeded.id },
  });
  const checkout = await call('POST', '/orders/checkout', { token: student });
  check('Şagird təlimi satın alır (201)', checkout.status === 201, `status=${checkout.status}`);

  const blocked = await call('DELETE', `/trainings/${seeded.id}`, { token: admin });
  check('Qeydiyyatı olan təlim silinmir (409)', blocked.status === 409, `status=${blocked.status}`);

  const stillThere = await call('GET', `/trainings/${seeded.id}`);
  check('Təlim silinməyib qalır', stillThere.status === 200);

  const shrink = await call('PUT', `/trainings/${seeded.id}`, {
    token: admin,
    body: {
      name: seeded.name,
      format: 'Live',
      description: 'Təsvir',
      price: seeded.price,
      durationHours: seeded.durationHours,
      metaLabel: seeded.metaLabel,
      seatLimit: 0,
    },
  });
  check('Yer limiti qeydiyyatdan aşağı salınmır', shrink.status === 400 || shrink.status === 409,
    `status=${shrink.status}`);

  // ------------------------------------- 3. Aldığım resurslar
  section('3. Aldığım resurslar (isPurchased / canAccess)');

  const catalog = await call('GET', '/resources?isPaid=true', { token: student });
  const paid = catalog.body?.items?.[0];
  check('Ödənişli resurs kataloqda var', Boolean(paid?.id));
  check('Alınmamış resursda canAccess = false', paid?.canAccess === false,
    `canAccess=${paid?.canAccess}`);
  check('Alınmamış resursda isPurchased = false', paid?.isPurchased === false);

  const emptyPurchases = await call('GET', '/resources/purchased', { token: student });
  check('Alınmış resurs siyahısı əvvəlcə boşdur', emptyPurchases.body?.length === 0,
    `count=${emptyPurchases.body?.length}`);

  await call('POST', '/cart/items', {
    token: student,
    body: { itemType: 'Resource', itemId: paid.id },
  });
  const buy = await call('POST', '/orders/checkout', { token: student });
  check('Resurs satın alınır (201)', buy.status === 201, `status=${buy.status}`);

  const purchased = await call('GET', '/resources/purchased', { token: student });
  check('Alınmış resurs siyahıda görünür', purchased.body?.length === 1,
    `count=${purchased.body?.length}`);
  check('Siyahıda canAccess = true', purchased.body?.[0]?.canAccess === true);
  check('Siyahıda isPurchased = true', purchased.body?.[0]?.isPurchased === true);

  const afterBuy = await call('GET', `/resources/${paid.id}`, { token: student });
  check('Detalda isPurchased = true', afterBuy.body?.isPurchased === true);
  check('Detalda canAccess = true', afterBuy.body?.canAccess === true);

  const anonView = await call('GET', `/resources/${paid.id}`);
  check('Anonim istifadəçidə canAccess = false', anonView.body?.canAccess === false);

  const reAdd = await call('POST', '/cart/items', {
    token: student,
    body: { itemType: 'Resource', itemId: paid.id },
  });
  check('Alınmış resurs yenidən səbətə düşmür (409)', reAdd.status === 409, `status=${reAdd.status}`);

  const purchasedAnon = await call('GET', '/resources/purchased');
  check('Anonim istifadəçi alınmışları görmür (401)', purchasedAnon.status === 401,
    `status=${purchasedAnon.status}`);

  // --------------------------------------------- 4. Admin icmalı
  section('4. Admin icmalı (dashboard)');

  // Audit log arxa planda batch şəklində yazılır (RequestLogOptions.FlushIntervalSeconds
  // default 5 saniyədir) - icmalı çağırmadan əvvəl qeydlərin bazaya düşməsini gözləyirik.
  await new Promise(resolve => setTimeout(resolve, 6000));

  const teacherDash = await call('GET', '/admin/dashboard', { token: teacher });
  check('Müəllim icmala girə bilmir (403)', teacherDash.status === 403, `status=${teacherDash.status}`);

  const dash = await call('GET', '/admin/dashboard', { token: admin });
  check('Admin icmalı 200 qaytarır', dash.status === 200, `status=${dash.status}`);

  const d = dash.body;
  check('Sorğu sayı doludur', d?.traffic?.totalRequests > 0, `total=${d?.traffic?.totalRequests}`);
  check('Uğursuz sorğular sayılır', d?.traffic?.failedRequests > 0,
    `failed=${d?.traffic?.failedRequests}`);
  check('Müştəri xətaları (4xx) sayılır', d?.traffic?.clientErrors > 0);
  check('Xəta faizi hesablanır', typeof d?.traffic?.errorRatePercent === 'number');
  check('Orta müddət hesablanır', d?.traffic?.averageDurationMs >= 0);

  check('İstifadəçi sayı doludur', d?.content?.totalUsers > 0, `users=${d?.content?.totalUsers}`);
  check('Resurs sayı doludur', d?.content?.totalResources > 0);
  check('Təsdiqlənmiş resurs sayılır', d?.content?.approvedResources > 0);
  check('Blog yazıları sayılır', d?.content?.blogPosts > 0);

  check('Təlim sayı doludur', d?.trainings?.totalTrainings > 0);
  check('Formata görə bölgü var',
    d?.trainings?.liveTrainings + d?.trainings?.onlineTrainings + d?.trainings?.videoTrainings
      === d?.trainings?.totalTrainings);
  check('Dərs sayı doludur', d?.trainings?.totalLessons > 0);
  check('Qeydiyyat sayılır', d?.trainings?.totalEnrollments > 0);
  check('Satılan təlim sayılır', d?.trainings?.soldTrainings === 1,
    `sold=${d?.trainings?.soldTrainings}`);

  check('GMV doludur', d?.sales?.gmv > 0, `gmv=${d?.sales?.gmv}`);
  check('Komissiya hesablanır', d?.sales?.commissionTotal > 0);
  check('Ödənilmiş sifariş sayılır', d?.sales?.paidOrders === 2, `orders=${d?.sales?.paidOrders}`);
  check('Satılan resurs sayılır', d?.sales?.soldResources === 1);

  check('Ən çox xəta verən ünvanlar gəlir', Array.isArray(d?.topErrors) && d.topErrors.length > 0);
  check('Xəta sətrində say var', d?.topErrors?.[0]?.count > 0);
  check('Populyar təlimlər gəlir', Array.isArray(d?.topTrainings) && d.topTrainings.length > 0);
  check('Təlim adı doludur', Boolean(d?.topTrainings?.[0]?.name));
  check('Hesabat vaxtı var', Boolean(d?.generatedAt));

  // ------------------------------- 5. Moderator resurs detalları
  section('5. Moderator resurs detallarını görür');

  const upload = await call('POST', '/resources/link', {
    token: teacher,
    body: {
      name: 'Moderasiya üçün ödənişli video',
      subject: 'Fizika',
      grade: 9,
      type: 'Video',
      externalUrl: 'https://www.youtube.com/watch?v=moderasiya',
      isPaid: true,
      price: 7,
    },
  });
  check('Ödənişli video yüklənir (201)', upload.status === 201, `status=${upload.status}`);

  const pendingList = await call('GET', '/admin/resources/pending', { token: admin });
  const row = pendingList.body?.find(r => r.id === upload.body?.id);
  check('Moderasiya növbəsində görünür', Boolean(row));
  check('Moderator linki görür', row?.externalUrl === 'https://www.youtube.com/watch?v=moderasiya',
    `externalUrl=${row?.externalUrl}`);
  check('Moderator müəllif adını görür', Boolean(row?.authorName));
  check('Moderasiya cavabı detal DTO-dur', row?.isLinkBased === true && 'rejectionReason' in row);

  const approved = await call('POST', `/admin/resources/${upload.body.id}/approve`, { token: admin });
  check('Təsdiq cavabında da link var',
    approved.body?.externalUrl === 'https://www.youtube.com/watch?v=moderasiya');

  console.log('\n==================================================');
  console.log(`NƏTİCƏ:  ${passed} keçdi, ${failed} uğursuz`);
  console.log('==================================================');

  process.exit(failed > 0 ? 1 : 0);
}

run().catch(err => {
  console.error('Test skripti xəta ilə dayandı:', err);
  process.exit(1);
});
