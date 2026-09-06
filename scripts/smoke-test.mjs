/**
 * MIMEDU.AZ — uçdan-uca smoke test.
 *
 * İşə salmadan əvvəl API-nin qalxdığından əmin olun:
 *   docker compose up -d
 *   dotnet run --project src/MimeduAz.Api
 *
 * Sonra:  node scripts/smoke-test.mjs
 *
 * Skript seed data ilə real axını yoxlayır: giriş, səbət, demo checkout,
 * komissiya hesablaması, imtahan, avtomatik sertifikat, moderasiya və icazələr.
 */

const BASE = process.env.MIMEDU_API ?? 'http://localhost:5297/api/v1';

const ID = {
  resFractions: '33333333-3333-3333-3333-333333333331',
  resGeometry: '33333333-3333-3333-3333-333333333332',
  resPhysics: '33333333-3333-3333-3333-333333333333',
  resEnglish: '33333333-3333-3333-3333-333333333336',
  trainAi: '44444444-4444-4444-4444-444444444441',
  trainClassroom: '44444444-4444-4444-4444-444444444443',
  quizFractions: '55555555-5555-5555-5555-555555555551',
  nigar: '22222222-2222-2222-2222-222222222221',
};

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

async function call(method, path, { token, body, raw } = {}) {
  const headers = {};
  if (token) headers.Authorization = `Bearer ${token}`;
  if (body !== undefined && !raw) headers['Content-Type'] = 'application/json';

  const res = await fetch(`${BASE}${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : raw ? body : JSON.stringify(body),
  });

  const text = await res.text();
  let json = null;
  try {
    json = text ? JSON.parse(text) : null;
  } catch {
    /* JSON deyil — status kifayətdir */
  }
  return { status: res.status, body: json, text };
}

function section(title) {
  console.log(`\n${title}`);
}

async function main() {
  // ---------------------------------------------------------------- 1. Publik
  section('1. Publik endpoint-lər');

  const blog = await call('GET', '/blog');
  check('GET /blog 3 yazı qaytarır', blog.status === 200 && blog.body?.length === 3, `status=${blog.status}`);

  const post = await call('GET', `/blog/${blog.body?.[0]?.id}`);
  check('GET /blog/{id} paraqrafları qaytarır', post.status === 200 && Array.isArray(post.body?.body) && post.body.body.length > 0);

  const verifyOk = await call('GET', '/certificates/verify/MIM-2026-4417');
  check('Sertifikat doğrulama (mövcud kod)', verifyOk.status === 200 && verifyOk.body?.isValid === true);
  check('Doğrulama sahibin adını qaytarır', verifyOk.body?.holderName === 'Nigar Əliyeva', verifyOk.body?.holderName);

  const verifyLower = await call('GET', '/certificates/verify/mim-2026-4417');
  check('Doğrulama böyük/kiçik hərfə həssas deyil', verifyLower.body?.isValid === true);

  const verifyBad = await call('GET', '/certificates/verify/MIM-2026-0000');
  check('Naməlum kod 200 + isValid:false qaytarır', verifyBad.status === 200 && verifyBad.body?.isValid === false);

  const resources = await call('GET', '/resources');
  check('GET /resources yalnız Approved qaytarır (5 ədəd)', resources.body?.totalCount === 5, `totalCount=${resources.body?.totalCount}`);
  check('Pending resurs siyahıda yoxdur', !resources.body?.items?.some((r) => r.id === ID.resEnglish));

  const filtered = await call('GET', '/resources?subject=Riyaziyyat&grade=5');
  check('Fənn+sinif filtri işləyir', filtered.body?.totalCount === 1 && filtered.body.items[0].grade === 5);

  const searched = await call('GET', '/resources?search=həndəsi');
  check('Axtarış böyük/kiçik hərfə həssas deyil', searched.body?.totalCount === 1, `totalCount=${searched.body?.totalCount}`);

  const trainings = await call('GET', '/trainings');
  check('GET /trainings 3 təlim qaytarır', trainings.body?.length === 3);
  const live = trainings.body?.find((t) => t.format === 'Live');
  check('Canlı təlimdə yer limiti var', live?.seatLimit === 25 && live?.seatsLeft === 24, `seatsLeft=${live?.seatsLeft}`);
  const video = trainings.body?.find((t) => t.format === 'Video');
  check('Video təlim limitsizdir', video?.seatLimit === null && video?.seatsLeft === null);

  const author = await call('GET', `/resources/author/${ID.nigar}`);
  check('Müəllif profili yalnız təsdiqlənmiş resursları göstərir', author.body?.resourceCount === 2);

  const anonCart = await call('GET', '/cart');
  check('Token olmadan /cart 401 verir', anonCart.status === 401, `status=${anonCart.status}`);

  // ------------------------------------------------------------------ 2. Auth
  section('2. Autentifikasiya');

  const badLogin = await call('POST', '/auth/login', { body: { email: 'nigar@mimedu.az', password: 'yanlis' } });
  check('Səhv şifrə 401 verir', badLogin.status === 401);

  const weak = await call('POST', '/auth/register', {
    body: { fullName: 'Zəif Şifrə', email: `weak-${Date.now()}@mimedu.az`, password: '123' },
  });
  check('Zəif şifrə 400 + errors qaytarır', weak.status === 400 && !!weak.body?.errors, `status=${weak.status}`);

  const teacher = await call('POST', '/auth/login', { body: { email: 'elvin@mimedu.az', password: 'Teacher123!' } });
  check('Müəllim girişi uğurludur', teacher.status === 200 && !!teacher.body?.accessToken);
  check('Müəllimin rolu Teacher-dir', teacher.body?.user?.roles?.includes('Teacher'));
  const teacherToken = teacher.body?.accessToken;

  const admin = await call('POST', '/auth/login', { body: { email: 'admin@mimedu.az', password: 'Admin123!' } });
  check('Admin girişi uğurludur', admin.status === 200);
  check('Admin rolu təyin olunub', admin.body?.user?.roles?.includes('Admin'));
  const adminToken = admin.body?.accessToken;

  const me = await call('GET', '/auth/me', { token: teacherToken });
  check('GET /auth/me profili qaytarır', me.body?.email === 'elvin@mimedu.az');

  const refreshed = await call('POST', '/auth/refresh', { body: { refreshToken: teacher.body?.refreshToken } });
  check('Refresh yeni token cütü verir', refreshed.status === 200 && refreshed.body?.refreshToken !== teacher.body?.refreshToken);

  const reused = await call('POST', '/auth/refresh', { body: { refreshToken: teacher.body?.refreshToken } });
  check('Köhnə refresh token təkrar işlədilə bilmir (rotasiya)', reused.status === 401, `status=${reused.status}`);

  const dupEmail = await call('POST', '/auth/register', {
    body: { fullName: 'Dublikat', email: 'elvin@mimedu.az', password: 'Test1234' },
  });
  check('Mövcud e-poçt ilə qeydiyyat 409 verir', dupEmail.status === 409);

  // -------------------------------------------------------- 3. Səbət/checkout
  section('3. Səbət və demo checkout');

  const freeToCart = await call('POST', '/cart/items', {
    token: teacherToken,
    body: { itemType: 'Resource', itemId: ID.resFractions },
  });
  check('Pulsuz resurs səbətə əlavə olunmur (400)', freeToCart.status === 400);

  const ownToCart = await call('POST', '/cart/items', {
    token: teacherToken,
    body: { itemType: 'Resource', itemId: ID.resPhysics },
  });
  check('Öz resursunu almaq olmur (400)', ownToCart.status === 400);

  const pendingToCart = await call('POST', '/cart/items', {
    token: teacherToken,
    body: { itemType: 'Resource', itemId: ID.resEnglish },
  });
  check('Təsdiqlənməmiş resurs səbətə düşmür (400)', pendingToCart.status === 400);

  const addResource = await call('POST', '/cart/items', {
    token: teacherToken,
    body: { itemType: 'Resource', itemId: ID.resGeometry },
  });
  check('Ödənişli resurs səbətə əlavə olunur', addResource.status === 200 && addResource.body?.items?.length === 1);
  check('Səbətdəki qiymət snapshot düzgündür', addResource.body?.items?.[0]?.price === 6.0, `price=${addResource.body?.items?.[0]?.price}`);

  const addAgain = await call('POST', '/cart/items', {
    token: teacherToken,
    body: { itemType: 'Resource', itemId: ID.resGeometry },
  });
  check('Eyni məhsul iki dəfə əlavə olunmur (409)', addAgain.status === 409);

  const addTraining = await call('POST', '/cart/items', {
    token: teacherToken,
    body: { itemType: 'Training', itemId: ID.trainClassroom },
  });
  check('Təlim səbətə əlavə olunur', addTraining.status === 200 && addTraining.body?.items?.length === 2);
  check('Səbətin cəmi düzgündür (6 + 45 = 51)', addTraining.body?.total === 51.0, `total=${addTraining.body?.total}`);

  const checkout = await call('POST', '/orders/checkout', { token: teacherToken, body: {} });
  check('Checkout 201 qaytarır', checkout.status === 201, `status=${checkout.status}`);
  check('Sifariş Paid statusundadır', checkout.body?.status === 'Paid');
  check('Sifariş kodu MIM-XXXX formatındadır', /^MIM-\d{4,6}$/.test(checkout.body?.code ?? ''), checkout.body?.code);
  check('Sifariş məbləği 51 AZN-dir', checkout.body?.totalAmount === 51.0);

  const emptyCart = await call('GET', '/cart', { token: teacherToken });
  check('Checkout-dan sonra səbət boşalır', emptyCart.body?.items?.length === 0);

  const myTrainings = await call('GET', '/trainings/mine', { token: teacherToken });
  check('Təlim üçün enrollment yaradılıb', myTrainings.body?.some((t) => t.trainingId === ID.trainClassroom));
  check('Enrollment InProgress statusundadır', myTrainings.body?.find((t) => t.trainingId === ID.trainClassroom)?.status === 'InProgress');

  const orders = await call('GET', '/orders/mine', { token: teacherToken });
  check('Sifariş tarixçəsi doludur', orders.body?.length === 1 && orders.body[0].items.length === 2);

  const boughtDownload = await call('POST', `/resources/${ID.resGeometry}/download`, { token: teacherToken });
  check('Satın alınmış resurs endirilir', boughtDownload.status === 200 && boughtDownload.body?.downloads === 269, `downloads=${boughtDownload.body?.downloads}`);

  const unpaidDownload = await call('POST', `/resources/${ID.resPhysics}/download`, {
    token: (await call('POST', '/auth/login', { body: { email: 'aysel@mimedu.az', password: 'Teacher123!' } })).body?.accessToken,
  });
  check('Alınmamış ödənişli resurs endirilmir (403)', unpaidDownload.status === 403, `status=${unpaidDownload.status}`);

  const freeDownload = await call('POST', `/resources/${ID.resFractions}/download`);
  check('Pulsuz resurs token olmadan endirilir', freeDownload.status === 200 && freeDownload.body?.downloads === 413, `downloads=${freeDownload.body?.downloads}`);

  // ------------------------------------------------------------- 4. Komissiya
  section('4. Komissiya hesablaması (20/80)');

  const sales = await call('GET', '/admin/sales', { token: adminToken });
  check('GMV 51 AZN-dir', sales.body?.gmv === 51.0, `gmv=${sales.body?.gmv}`);
  check('Komissiya 1.20 AZN-dir (6 × 20%)', sales.body?.commissionTotal === 1.2, `commission=${sales.body?.commissionTotal}`);
  check('Müəllif payı 4.80 AZN-dir (6 × 80%)', sales.body?.authorPayoutTotal === 4.8, `payout=${sales.body?.authorPayoutTotal}`);
  check('Təlimdən komissiya tutulmur', sales.body?.trainingRevenue === 45.0 && sales.body?.resourceRevenue === 6.0);
  check('Komissiya faizi konfiqurasiyadan gəlir', sales.body?.commissionPercent === 0.2);

  // ------------------------------------------------------------------ 5. Quiz
  section('5. İmtahan və avtomatik sertifikat');

  const quizMeta = await call('GET', `/resources/${ID.resFractions}/quiz`);
  check('Quiz metadata-sı qaytarılır', quizMeta.status === 200 && quizMeta.body?.questionCount === 4);
  check('Keçid balı 70%-dir', quizMeta.body?.passPercent === 70);

  const questions = await call('GET', `/quiz/${ID.quizFractions}/questions`, { token: teacherToken });
  check('4 sual qaytarılır', questions.body?.length === 4);
  check('Düzgün cavabın indeksi cavabda YOXDUR', !JSON.stringify(questions.body).toLowerCase().includes('correct'), JSON.stringify(questions.body).slice(0, 120));

  const anonQuestions = await call('GET', `/quiz/${ID.quizFractions}/questions`);
  check('Suallar token olmadan alınmır (401)', anonQuestions.status === 401);

  // Seed-dəki düzgün cavablar: 1, 2, 0, 1
  const correct = [1, 2, 0, 1];
  const sorted = [...(questions.body ?? [])].sort((a, b) => a.orderIndex - b.orderIndex);

  const failAttempt = await call('POST', `/quiz/${ID.quizFractions}/submit`, {
    token: teacherToken,
    body: { answers: sorted.map((q, i) => ({ questionId: q.id, selectedIndex: i === 0 ? correct[0] : (correct[i] + 1) % 4 })) },
  });
  check('Aşağı nəticə keçmir', failAttempt.body?.passed === false && failAttempt.body?.scorePercent === 25, `score=${failAttempt.body?.scorePercent}`);
  check('Uğursuz cəhddə sertifikat verilmir', failAttempt.body?.certificateCode === undefined || failAttempt.body?.certificateCode === null);

  const passAttempt = await call('POST', `/quiz/${ID.quizFractions}/submit`, {
    token: teacherToken,
    body: { answers: sorted.map((q, i) => ({ questionId: q.id, selectedIndex: correct[i] })) },
  });
  check('Tam düzgün cavab 100% verir', passAttempt.body?.scorePercent === 100 && passAttempt.body?.passed === true);
  check('Sertifikat avtomatik verilir', /^MIM-\d{4}-\d{4,6}$/.test(passAttempt.body?.certificateCode ?? ''), passAttempt.body?.certificateCode);

  const newCert = await call('GET', `/certificates/verify/${passAttempt.body?.certificateCode}`);
  check('Yeni sertifikat publik doğrulanır', newCert.body?.isValid === true && newCert.body?.holderName === 'Elvin Məmmədov');

  const myCerts = await call('GET', '/certificates/mine', { token: teacherToken });
  check('Sertifikat "mənim sertifikatlarım"da görünür', myCerts.body?.length === 1);

  const foreignQuiz = await call('POST', `/resources/${ID.resFractions}/quiz`, {
    token: teacherToken,
    body: { passPercent: 70, mode: 'append', questions: [{ questionText: 'X', options: ['A', 'B'], correctOptionIndex: 0 }] },
  });
  check('Başqasının resursuna quiz əlavə olunmur (403)', foreignQuiz.status === 403, `status=${foreignQuiz.status}`);

  const ownQuiz = await call('POST', `/resources/${ID.resPhysics}/quiz`, {
    token: teacherToken,
    body: {
      passPercent: 80,
      mode: 'append',
      questions: [{ questionText: 'Enerji vahidi?', options: ['J', 'N', 'W', 'A'], correctOptionIndex: 0 }],
    },
  });
  check('Öz resursuna sual əlavə olunur (append)', ownQuiz.status === 200 && ownQuiz.body?.questionCount === 4, `count=${ownQuiz.body?.questionCount}`);

  const replaced = await call('POST', `/resources/${ID.resPhysics}/quiz`, {
    token: teacherToken,
    body: {
      passPercent: 75,
      mode: 'replace',
      questions: [{ questionText: 'Sürət vahidi?', options: ['m/s', 'N'], correctOptionIndex: 0 }],
    },
  });
  check('replace rejimi köhnə sualları silir', replaced.body?.questionCount === 1, `count=${replaced.body?.questionCount}`);

  const badIndex = await call('POST', `/resources/${ID.resPhysics}/quiz`, {
    token: teacherToken,
    body: { passPercent: 70, mode: 'append', questions: [{ questionText: 'X', options: ['A', 'B'], correctOptionIndex: 5 }] },
  });
  check('Sərhəddən kənar correctOptionIndex 400 verir', badIndex.status === 400);

  // ----------------------------------------------------------------- 6. Admin
  section('6. Admin panel və moderasiya');

  const forbidden = await call('GET', '/admin/users', { token: teacherToken });
  check('Müəllim admin endpoint-inə girə bilmir (403)', forbidden.status === 403);

  const pending = await call('GET', '/admin/resources/pending', { token: adminToken });
  check('Moderasiya növbəsində 1 resurs var', pending.body?.length === 1 && pending.body[0].id === ID.resEnglish);

  const approve = await call('POST', `/admin/resources/${ID.resEnglish}/approve`, { token: adminToken });
  check('Resurs təsdiqlənir', approve.body?.status === 'Approved' && !!approve.body?.approvedAt);

  const afterApprove = await call('GET', '/resources');
  check('Təsdiqlənmiş resurs Resurs Bankında görünür', afterApprove.body?.totalCount === 6);

  const reject = await call('POST', `/admin/resources/${ID.resEnglish}/reject`, {
    token: adminToken,
    body: { reason: 'Orfoqrafik səhvlər var.' },
  });
  check('Resurs rədd edilir və səbəb saxlanılır', reject.body?.status === 'Rejected' && reject.body?.rejectionReason === 'Orfoqrafik səhvlər var.');

  const afterReject = await call('GET', '/resources');
  check('Rədd edilmiş resurs siyahıdan çıxır', afterReject.body?.totalCount === 5);

  const users = await call('GET', '/admin/users', { token: adminToken });
  check('Admin bütün istifadəçiləri görür', users.body?.length >= 7, `count=${users.body?.length}`);
  const nigarRow = users.body?.find((u) => u.id === ID.nigar);
  check('İstifadəçi statistikası doludur', nigarRow?.resourceCount === 2 && nigarRow?.totalDownloads > 0);
  check('Rollar düzgün doldurulub', users.body?.find((u) => u.email === 'admin@mimedu.az')?.roles?.includes('Admin'));

  const adminOrders = await call('GET', '/admin/orders', { token: adminToken });
  check('Admin bütün sifarişləri görür', adminOrders.body?.length === 1);

  const trainingByTeacher = await call('POST', '/trainings', {
    token: teacherToken,
    body: { name: 'İcazəsiz', format: 'Video', description: 'test', price: 1, durationHours: 1, metaLabel: '1 dərs', syllabus: [] },
  });
  check('Müəllim təlim yarada bilmir (403)', trainingByTeacher.status === 403);

  const newTraining = await call('POST', '/trainings', {
    token: adminToken,
    body: {
      name: 'Rəqəmsal differensiasiya',
      format: 'Online',
      description: 'Onlayn təlim',
      price: 60,
      durationHours: 10,
      metaLabel: '5 sessiya',
      syllabus: ['Giriş', 'Alətlər', 'Tətbiq'],
    },
  });
  check('Admin təlim yaradır', newTraining.status === 201 && newTraining.body?.syllabus?.length === 3);
  check('Onlayn təlimdə yer limiti null olur', newTraining.body?.seatLimit === null);

  const manualCert = await call('POST', '/certificates/issue', {
    token: adminToken,
    body: { userFullName: 'Nigar Əliyeva', trainingId: ID.trainAi },
  });
  check('Admin əl ilə sertifikat verir', manualCert.status === 201 && /^MIM-\d{4}-\d{4,6}$/.test(manualCert.body?.code ?? ''));
  check('Sertifikat təsviri düzgün formatdadır', (manualCert.body?.description ?? '').includes('Nigar Əliyeva') && manualCert.body.description.includes('8 saat'));

  const unknownUser = await call('POST', '/certificates/issue', {
    token: adminToken,
    body: { userFullName: 'Mövcud Olmayan', trainingId: ID.trainAi },
  });
  check('Naməlum istifadəçiyə sertifikat verilmir (404)', unknownUser.status === 404);

  // ---------------------------------------------------------- 7. Fayl yükləmə
  section('7. Fayl yükləmə və moderasiya axını');

  const upload = async (fileName, mime, sizeBytes = 2048) => {
    const form = new FormData();
    form.append('Name', 'Smoke test iş vərəqi');
    form.append('Subject', 'Kimya');
    form.append('Grade', '9');
    form.append('Type', 'WorkSheet');
    form.append('IsPaid', 'false');
    form.append('Price', '0');
    form.append('file', new Blob([new Uint8Array(sizeBytes)], { type: mime }), fileName);

    const res = await fetch(`${BASE}/resources`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${teacherToken}` },
      body: form,
    });
    const text = await res.text();
    return { status: res.status, body: text ? JSON.parse(text) : null };
  };

  const badExt = await upload('virus.exe', 'application/octet-stream');
  check('İcazəsiz fayl formatı rədd olunur (400)', badExt.status === 400, `status=${badExt.status}`);

  const uploaded = await upload('numune.pdf', 'application/pdf');
  check('PDF yüklənir (201)', uploaded.status === 201, `status=${uploaded.status}`);
  check('Yeni resurs Pending statusunda başlayır', uploaded.body?.status === 'Pending');
  check('Qiymət pulsuz resursda 0-dır', uploaded.body?.price === 0);

  const notPublic = await call('GET', '/resources');
  check('Pending resurs ictimai siyahıda yoxdur', !notPublic.body?.items?.some((r) => r.id === uploaded.body?.id));

  const mine = await call('GET', '/resources/mine', { token: teacherToken });
  check('Müəllif öz Pending resursunu görür', mine.body?.some((r) => r.id === uploaded.body?.id));

  const approveUploaded = await call('POST', `/admin/resources/${uploaded.body?.id}/approve`, { token: adminToken });
  check('Yüklənmiş resurs təsdiqlənir', approveUploaded.body?.status === 'Approved');

  const download = await call('POST', `/resources/${uploaded.body?.id}/download`);
  check('Təsdiqlənmiş resurs endirilir', download.status === 200 && download.body?.downloadUrl?.startsWith('/uploads/resources/'), download.body?.downloadUrl);

  const staticFile = await fetch(`${BASE.replace('/api/v1', '')}${download.body?.downloadUrl}`);
  check('Fayl statik olaraq verilir', staticFile.status === 200, `status=${staticFile.status}`);

  // -------------------------------------------------------------- 8. Blog CRUD
  section('8. Blog CMS (Admin)');

  const blogByTeacher = await call('POST', '/blog', {
    token: teacherToken,
    body: { title: 'İcazəsiz', tag: 'X', readTime: '1 dəq', excerpt: 'x', body: ['x'] },
  });
  check('Müəllim blog yazısı əlavə edə bilmir (403)', blogByTeacher.status === 403);

  const emptyBody = await call('POST', '/blog', {
    token: adminToken,
    body: { title: 'Boş gövdə', tag: 'Test', readTime: '2 dəq', excerpt: 'test', body: [] },
  });
  check('Paraqrafsız yazı 400 verir', emptyBody.status === 400);

  const created = await call('POST', '/blog', {
    token: adminToken,
    body: {
      title: 'Smoke test yazısı',
      tag: 'Test',
      readTime: '3 dəq',
      excerpt: 'Qısa təsvir',
      body: ['Birinci paraqraf.', 'İkinci paraqraf.'],
    },
  });
  check('Admin blog yazısı yaradır (201)', created.status === 201 && created.body?.body?.length === 2);

  const updated = await call('PUT', `/blog/${created.body?.id}`, {
    token: adminToken,
    body: {
      title: 'Yenilənmiş başlıq',
      tag: 'Test',
      readTime: '4 dəq',
      excerpt: 'Yenilənmiş təsvir',
      body: ['Tək paraqraf.'],
    },
  });
  check('Admin yazını yeniləyir', updated.body?.title === 'Yenilənmiş başlıq' && updated.body?.body?.length === 1);

  const listAfterCreate = await call('GET', '/blog');
  check('Yeni yazı siyahıda görünür', listAfterCreate.body?.length === 4);

  const deleted = await call('DELETE', `/blog/${created.body?.id}`, { token: adminToken });
  check('Admin yazını silir (204)', deleted.status === 204);

  const listAfterDelete = await call('GET', '/blog');
  check('Silinmiş yazı siyahıdan çıxır', listAfterDelete.body?.length === 3);

  const deleteAgain = await call('DELETE', `/blog/${created.body?.id}`, { token: adminToken });
  check('Silinmiş yazının təkrar silinməsi 404 verir', deleteAgain.status === 404);

  // -------------------------------------------------------------- 9. Xəta formatı
  section('9. Xəta formatı');

  const notFound = await call('GET', '/resources/00000000-0000-0000-0000-000000000000');
  check('404 standart formatda qayıdır', notFound.status === 404 && !!notFound.body?.message && !!notFound.body?.traceId, notFound.text);

  const invalid = await call('POST', '/cart/items', {
    token: teacherToken,
    body: { itemType: 'Resource', itemId: '9f3c1a52-0000-4000-8000-abcdefabcdef' },
  });
  check('Mövcud olmayan məhsul 404 verir', invalid.status === 404, `status=${invalid.status}`);

  const emptyGuid = await call('POST', '/cart/items', {
    token: teacherToken,
    body: { itemType: 'Resource', itemId: '00000000-0000-0000-0000-000000000000' },
  });
  check('Boş Guid validasiyada 400 verir', emptyGuid.status === 400 && !!emptyGuid.body?.errors);

  console.log(`\n${'='.repeat(50)}`);
  console.log(`NƏTİCƏ:  ${passed} keçdi, ${failed} uğursuz`);
  console.log('='.repeat(50));
  process.exit(failed === 0 ? 0 : 1);
}

main().catch((err) => {
  console.error('Smoke test icra oluna bilmədi:', err);
  process.exit(1);
});
