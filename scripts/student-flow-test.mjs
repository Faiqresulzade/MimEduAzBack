/**
 * MIMEDU.AZ — şagird axınının uçdan-uca yoxlanışı.
 *
 * Qeydiyyat (şagird) → təlim alma → dərslərə baxış → dərs-dərs tamamlama →
 * avtomatik sertifikat → rol məhdudiyyətləri.
 *
 * İşə salmaq:  node scripts/student-flow-test.mjs
 */

const BASE = process.env.MIMEDU_API ?? 'http://localhost:5297/api/v1';

const ID = {
  trainClassroom: '44444444-4444-4444-4444-444444444443', // Video kurs, 4 dərs
  trainAi: '44444444-4444-4444-4444-444444444441',
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
  // ------------------------------------------------------------ 1. Şagird qeydiyyatı
  section('1. Şagird hesabı');

  const email = `sagird-${Date.now()}@mimedu.az`;
  const reg = await call('POST', '/auth/register', {
    body: { fullName: 'Şagird Test', email, password: 'Test12345' },
  });
  check('AccountType göndərilməsə şagird yaranır', reg.status === 200 && reg.body?.user?.roles?.includes('Student'),
    `status=${reg.status} roles=${reg.body?.user?.roles}`);
  check('canPublishResources = false', reg.body?.user?.canPublishResources === false);

  const student = reg.body?.accessToken;

  const teacherReg = await call('POST', '/auth/register', {
    body: { fullName: 'Müəllif Test', email: `muellif-${Date.now()}@mimedu.az`, password: 'Test12345', subject: 'Fizika', accountType: 'Teacher' },
  });
  check('accountType=Teacher ilə müəllif yaranır', teacherReg.body?.user?.roles?.includes('Teacher'));
  check('Müəllifdə canPublishResources = true', teacherReg.body?.user?.canPublishResources === true);

  const seedStudent = await call('POST', '/auth/login', { body: { email: 'sevinc@mimedu.az', password: 'Student123!' } });
  check('Seed şagird hesabı ilə giriş', seedStudent.status === 200 && seedStudent.body?.user?.roles?.includes('Student'));

  // ------------------------------------------------------- 2. Rol məhdudiyyəti
  section('2. Rol məhdudiyyəti (material yükləmə)');

  const form = new FormData();
  form.append('Name', 'Şagird yükləməsi');
  form.append('Subject', 'Kimya');
  form.append('Grade', '9');
  form.append('Type', 'WorkSheet');
  form.append('IsPaid', 'false');
  form.append('Price', '0');
  form.append('file', new Blob([new Uint8Array(1024)], { type: 'application/pdf' }), 'test.pdf');

  const upload = await fetch(`${BASE}/resources`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${student}` },
    body: form,
  });
  check('Şagird material yükləyə bilmir (403)', upload.status === 403, `status=${upload.status}`);

  // --------------------------------------------------- 3. Dərslərə giriş nəzarəti
  section('3. Dərslərə giriş nəzarəti');

  const beforeEnroll = await call('GET', `/trainings/${ID.trainClassroom}/lessons`, { token: student });
  check('Yazılmadan dərslər görünmür (403)', beforeEnroll.status === 403, `status=${beforeEnroll.status}`);

  const anonLessons = await call('GET', `/trainings/${ID.trainClassroom}/lessons`);
  check('Token olmadan dərslər görünmür (401)', anonLessons.status === 401);

  const detailBefore = await call('GET', `/trainings/${ID.trainClassroom}`, { token: student });
  check('Detalda isEnrolled = false', detailBefore.body?.isEnrolled === false);
  check('Detalda dərs sayı göstərilir', detailBefore.body?.lessonCount === 4, `lessonCount=${detailBefore.body?.lessonCount}`);

  // ------------------------------------------------------------ 4. Təlim alma
  section('4. Təlim alma (səbət → checkout)');

  const addCart = await call('POST', '/cart/items', {
    token: student,
    body: { itemType: 'Training', itemId: ID.trainClassroom },
  });
  check('Şagird təlimi səbətə əlavə edir', addCart.status === 200);

  const checkout = await call('POST', '/orders/checkout', { token: student, body: {} });
  check('Checkout uğurlu (201)', checkout.status === 201, `status=${checkout.status}`);

  const detailAfter = await call('GET', `/trainings/${ID.trainClassroom}`, { token: student });
  check('Alışdan sonra isEnrolled = true', detailAfter.body?.isEnrolled === true);

  // ------------------------------------------------------------- 5. Dərslər
  section('5. Dərslərə baxış');

  const lessons = await call('GET', `/trainings/${ID.trainClassroom}/lessons`, { token: student });
  check('Dərslər açılır (200)', lessons.status === 200);
  check('4 dərs qayıdır', lessons.body?.lessons?.length === 4);
  check('Video linki verilir', typeof lessons.body?.lessons?.[0]?.videoUrl === 'string'
    && lessons.body.lessons[0].videoUrl.startsWith('http'), lessons.body?.lessons?.[0]?.videoUrl);
  check('Dərslər sıra ilə gəlir', lessons.body?.lessons?.every((l, i) => l.orderIndex === i));
  check('Başlanğıcda heç bir dərs tamamlanmayıb', lessons.body?.completedLessonCount === 0 && lessons.body?.progressPercent === 0);
  check('Müddət göstərilir', typeof lessons.body?.lessons?.[0]?.durationMinutes === 'number');

  const ids = lessons.body.lessons.map((l) => l.id);

  // ------------------------------------------------- 6. İrəliləyiş və sertifikat
  section('6. Dərs-dərs tamamlama və avtomatik sertifikat');

  const p1 = await call('POST', `/trainings/${ID.trainClassroom}/lessons/${ids[0]}/complete`, { token: student });
  check('1-ci dərs → 25%', p1.body?.progressPercent === 25 && p1.body?.completedLessonCount === 1,
    `percent=${p1.body?.progressPercent}`);
  check('Hələ sertifikat yoxdur', p1.body?.certificateCode === null || p1.body?.certificateCode === undefined);
  check('Status InProgress', p1.body?.status === 'InProgress');

  const again = await call('POST', `/trainings/${ID.trainClassroom}/lessons/${ids[0]}/complete`, { token: student });
  check('Eyni dərsin təkrar tamamlanması faizi artırmır', again.body?.progressPercent === 25,
    `percent=${again.body?.progressPercent}`);

  const undo = await call('DELETE', `/trainings/${ID.trainClassroom}/lessons/${ids[0]}/complete`, { token: student });
  check('İşarəni geri götürmək → 0%', undo.body?.progressPercent === 0 && undo.body?.completedLessonCount === 0);

  await call('POST', `/trainings/${ID.trainClassroom}/lessons/${ids[0]}/complete`, { token: student });
  await call('POST', `/trainings/${ID.trainClassroom}/lessons/${ids[1]}/complete`, { token: student });
  const p3 = await call('POST', `/trainings/${ID.trainClassroom}/lessons/${ids[2]}/complete`, { token: student });
  check('3/4 dərs → 75%', p3.body?.progressPercent === 75, `percent=${p3.body?.progressPercent}`);
  check('75%-də hələ sertifikat yoxdur', !p3.body?.certificateCode);

  const p4 = await call('POST', `/trainings/${ID.trainClassroom}/lessons/${ids[3]}/complete`, { token: student });
  check('Son dərs → 100%', p4.body?.progressPercent === 100 && p4.body?.completedLessonCount === 4);
  check('Status Completed', p4.body?.status === 'Completed');
  check('Sertifikat avtomatik verilir', /^MIM-\d{4}-\d{4,6}$/.test(p4.body?.certificateCode ?? ''), p4.body?.certificateCode);

  const verify = await call('GET', `/certificates/verify/${p4.body?.certificateCode}`);
  check('Sertifikat publik doğrulanır', verify.body?.isValid === true && verify.body?.holderName === 'Şagird Test');

  const myCerts = await call('GET', '/certificates/mine', { token: student });
  check('Sertifikat "mənim sertifikatlarım"da', myCerts.body?.some((c) => c.code === p4.body?.certificateCode));

  // ------------------------------------------------------- 7. Mənim təlimlərim
  section('7. Mənim təlimlərim');

  const mine = await call('GET', '/trainings/mine', { token: student });
  const row = mine.body?.find((t) => t.trainingId === ID.trainClassroom);
  check('Təlim siyahıda görünür', !!row);
  check('İrəliləyiş 100%', row?.progressPercent === 100);
  check('Dərs sayğacı düzgündür (4/4)', row?.completedLessonCount === 4 && row?.totalLessonCount === 4);
  check('Status Completed və completedAt dolu', row?.status === 'Completed' && !!row?.completedAt);
  check('Sertifikat kodu siyahıda', row?.certificateCode === p4.body?.certificateCode);

  // ------------------------------------------------- 8. Şagird → müəllif keçidi
  section('8. Şagird müəllif olur');

  const become = await call('POST', '/auth/become-author', { token: student, body: { subject: 'Kimya' } });
  check('become-author 200 qaytarır', become.status === 200);
  check('Teacher rolu əlavə olunur', become.body?.roles?.includes('Teacher'));
  check('canPublishResources = true olur', become.body?.canPublishResources === true);
  check('Şagird rolu itmir', become.body?.roles?.includes('Student'));

  const idem = await call('POST', '/auth/become-author', { token: student, body: {} });
  check('Təkrar çağırış idempotentdir', idem.status === 200 && idem.body?.canPublishResources === true);

  console.log(`\n${'='.repeat(50)}`);
  console.log(`NƏTİCƏ:  ${passed} keçdi, ${failed} uğursuz`);
  console.log('='.repeat(50));
  process.exit(failed === 0 ? 0 : 1);
}

main().catch((err) => {
  console.error('Test icra oluna bilmədi:', err);
  process.exit(1);
});
