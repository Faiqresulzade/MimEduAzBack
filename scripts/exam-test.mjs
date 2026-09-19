/**
 * MIMEDU.AZ — sınaq (imtahan) modulunun uçdan-uca testi.
 *
 * TƏMİZ baza ilə işə salın:
 *   docker compose down -v && docker compose up -d
 *   dotnet ef database update --project src/MimeduAz.Infrastructure --startup-project src/MimeduAz.Api
 *   dotnet run --project src/MimeduAz.Api
 *
 * Sonra:  node scripts/exam-test.mjs
 */

const BASE = process.env.MIMEDU_API ?? 'http://localhost:5297/api/v1';

const ID = {
  examMathFree: '99999999-9999-9999-9999-999999999991',
  examGraduation: '99999999-9999-9999-9999-999999999992',
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

/** Cavab açarından istifadə edərək bütün sualları düzgün cavablandırır. */
function answerAll(run, correctByQuestionId) {
  return run.sections.flatMap(s =>
    s.questions.map(q => ({
      questionId: q.id,
      selectedIndex: correctByQuestionId[q.id],
    })));
}

async function run() {
  const admin = await login('admin@mimedu.az', 'Admin123!');
  const nigar = await login('nigar@mimedu.az', 'Teacher123!');
  const elvin = await login('elvin@mimedu.az', 'Teacher123!');
  const student = await login('sevinc@mimedu.az', 'Student123!');
  const tural = await login('tural@mimedu.az', 'Student123!');

  check('Admin daxil olur', Boolean(admin));
  check('Müəllim (Nigar) daxil olur', Boolean(nigar));
  check('Müəllim (Elvin) daxil olur', Boolean(elvin));
  check('Şagird (Sevinc) daxil olur', Boolean(student));

  // ------------------------------------------------- 1. Kataloq
  section('1. Sınaq kataloqu');

  const catalog = await call('GET', '/exams');
  check('Kataloq 200 qaytarır', catalog.status === 200, `status=${catalog.status}`);
  check('Seed-dən 2 sınaq gəlir', catalog.body?.totalCount === 2,
    `totalCount=${catalog.body?.totalCount}`);

  const free = catalog.body?.items?.find(e => e.id === ID.examMathFree);
  const paid = catalog.body?.items?.find(e => e.id === ID.examGraduation);

  check('Pulsuz sınaqda canAccess = true', free?.canAccess === true);
  check('Pulsuz sınağın müddəti 10 dəqiqədir', free?.durationMinutes === 10);
  check('Pulsuz sınaqda 3 sual var', free?.questionCount === 3, `count=${free?.questionCount}`);
  check('Ödənişli sınaqda 2 bölmə var', paid?.sectionCount === 2, `count=${paid?.sectionCount}`);
  check('Ödənişli sınaqda 4 sual var', paid?.questionCount === 4);
  check('Anonim istifadəçidə ödənişli canAccess = false', paid?.canAccess === false);

  const filtered = await call('GET', '/exams?subject=Riyaziyyat');
  check('Fənn filtri işləyir', filtered.body?.totalCount === 1, `count=${filtered.body?.totalCount}`);

  const detail = await call('GET', `/exams/${ID.examGraduation}`);
  check('Detal 200 qaytarır', detail.status === 200);
  check('Detalda bölmələr fənn adı ilə gəlir',
    detail.body?.sections?.map(s => s.subject).join(',') === 'Riyaziyyat,Fizika',
    detail.body?.sections?.map(s => s.subject).join(','));
  check('Detalda suallar AÇIQ GÖNDƏRİLMİR', detail.body?.questions === undefined);

  // ------------------------------------------------- 2. Pulsuz sınağın verilməsi
  section('2. Pulsuz sınağın verilməsi (vaxt limiti)');

  const startedAnon = await call('POST', `/exams/${ID.examMathFree}/start`);
  check('Token olmadan başlamaq olmur (401)', startedAnon.status === 401,
    `status=${startedAnon.status}`);

  const started = await call('POST', `/exams/${ID.examMathFree}/start`, { token: student });
  check('Şagird pulsuz sınağa başlayır (200)', started.status === 200, `status=${started.status}`);

  const runDto = started.body;
  check('Cəhd id-si qayıdır', Boolean(runDto?.attemptId));
  check('expiresAt doludur', Boolean(runDto?.expiresAt));
  check('Qalan vaxt ~10 dəqiqədir',
    runDto?.remainingSeconds > 570 && runDto?.remainingSeconds <= 600,
    `remaining=${runDto?.remainingSeconds}`);
  check('3 sual qayıdır', runDto?.questionCount === 3);
  check('Düzgün cavab indeksi GİZLİDİR',
    runDto?.sections?.[0]?.questions?.every(q => q.correctOptionIndex === undefined));
  check('Variantlar gəlir', runDto?.sections?.[0]?.questions?.[0]?.options?.length === 4);
  check('LaTeX sualı olduğu kimi saxlanılır',
    runDto?.sections?.[0]?.questions?.some(q => q.questionText.includes('$x^2 = 49$')));

  const restart = await call('POST', `/exams/${ID.examMathFree}/start`, { token: student });
  check('Təkrar başlatma eyni cəhdi davam etdirir',
    restart.body?.attemptId === runDto.attemptId, `attemptId=${restart.body?.attemptId}`);
  check('Vaxt sıfırlanmır', restart.body?.remainingSeconds <= runDto.remainingSeconds);

  // Seed cavab açarı: 1/2+1/4 → indeks 1, 200-ün 15%-i → indeks 2, x²=49 → indeks 2
  const mathAnswers = runDto.sections[0].questions.map((q, i) => ({
    questionId: q.id,
    selectedIndex: [1, 2, 2][i],
  }));

  const submitted = await call('POST', `/exams/attempts/${runDto.attemptId}/submit`, {
    token: student,
    body: { answers: mathAnswers },
  });

  check('Təhvil 200 qaytarır', submitted.status === 200, `status=${submitted.status}`);
  check('Nəticə 100%', submitted.body?.scorePercent === 100, `score=${submitted.body?.scorePercent}`);
  check('3/3 düzgündür', submitted.body?.correctCount === 3);
  check('Keçdi', submitted.body?.passed === true);
  check('Status Submitted', submitted.body?.status === 'Submitted');
  check('Sertifikat avtomatik verilir', Boolean(submitted.body?.certificateCode),
    `code=${submitted.body?.certificateCode}`);
  check('Sərf olunan vaxt yazılır', submitted.body?.elapsedSeconds >= 0);
  check('Cavab açarı nəticədə açılır',
    submitted.body?.sections?.[0]?.questions?.[0]?.correctOptionIndex === 1);
  check('Seçilən cavab göstərilir',
    submitted.body?.sections?.[0]?.questions?.[0]?.selectedIndex === 1);

  const certCode = submitted.body?.certificateCode;
  const verify = await call('GET', `/certificates/verify/${certCode}`);
  check('Sınaq sertifikatı publik doğrulanır', verify.body?.isValid === true);
  check('Sertifikat təsvirində «sınağı» sözü var',
    verify.body?.description?.includes('sınağı'), verify.body?.description);

  const reSubmit = await call('POST', `/exams/attempts/${runDto.attemptId}/submit`, {
    token: student,
    body: { answers: mathAnswers },
  });
  check('Təkrar təhvil 409 verir', reSubmit.status === 409, `status=${reSubmit.status}`);

  const reStart = await call('POST', `/exams/${ID.examMathFree}/start`, { token: student });
  check('Verilmiş sınaq təkrar başladılmır (409)', reStart.status === 409, `status=${reStart.status}`);

  const attempts = await call('GET', '/exams/attempts/mine', { token: student });
  check('Cəhdlərim siyahısında 1 sətir var', attempts.body?.length === 1);
  check('Siyahıda sertifikat kodu var', attempts.body?.[0]?.certificateCode === certCode);

  // ------------------------------------------------- 3. Ödənişli sınaq
  section('3. Ödənişli sınaq (satın alma tələbi)');

  const blocked = await call('POST', `/exams/${ID.examGraduation}/start`, { token: student });
  check('Almadan başlamaq olmur (403)', blocked.status === 403, `status=${blocked.status}`);

  const emptyPurchased = await call('GET', '/exams/purchased', { token: student });
  check('Alınmış sınaq siyahısı əvvəlcə boşdur', emptyPurchased.body?.length === 0);

  await call('POST', '/cart/items', {
    token: student,
    body: { itemType: 'Exam', itemId: ID.examGraduation },
  });
  const checkout = await call('POST', '/orders/checkout', { token: student });
  check('Sınaq satın alınır (201)', checkout.status === 201, `status=${checkout.status}`);
  check('Sifarişdə sınaq sətri var',
    checkout.body?.items?.some(i => i.itemType === 'Exam'));
  // Komissiya OrderItemDto-da göndərilmir (admin məlumatıdır) - aşağıda
  // /admin/sales üzərindən yoxlanılır.
  check('Sifariş məbləği sınağın qiymətidir',
    checkout.body?.items?.find(i => i.itemType === 'Exam')?.price === 9.90,
    `price=${checkout.body?.items?.find(i => i.itemType === 'Exam')?.price}`);

  const purchased = await call('GET', '/exams/purchased', { token: student });
  check('Alınmış sınaq siyahıda görünür', purchased.body?.length === 1);
  check('Alınmışda canAccess = true', purchased.body?.[0]?.canAccess === true);
  check('Alınmışda isPurchased = true', purchased.body?.[0]?.isPurchased === true);

  const reAdd = await call('POST', '/cart/items', {
    token: student,
    body: { itemType: 'Exam', itemId: ID.examGraduation },
  });
  check('Alınmış sınaq yenidən səbətə düşmür (409)', reAdd.status === 409, `status=${reAdd.status}`);

  const paidRun = await call('POST', `/exams/${ID.examGraduation}/start`, { token: student });
  check('Alındıqdan sonra başlayır (200)', paidRun.status === 200, `status=${paidRun.status}`);
  check('İki bölmə qayıdır', paidRun.body?.sections?.length === 2);
  check('Müddət 90 dəqiqədir', paidRun.body?.durationMinutes === 90);

  // Yalnız riyaziyyat bölməsini düzgün cavablandırırıq → fənn üzrə fərq görünsün.
  const mathPart = paidRun.body.sections.find(s => s.subject === 'Riyaziyyat');
  const physicsPart = paidRun.body.sections.find(s => s.subject === 'Fizika');

  const partialAnswers = [
    ...mathPart.questions.map((q, i) => ({ questionId: q.id, selectedIndex: [1, 2][i] })),
    // Fizikada qəsdən səhv cavab veririk (düzgünlər 0 və 2).
    ...physicsPart.questions.map(q => ({ questionId: q.id, selectedIndex: 3 })),
  ];

  const paidResult = await call('POST', `/exams/attempts/${paidRun.body.attemptId}/submit`, {
    token: student,
    body: { answers: partialAnswers },
  });

  check('Ödənişli sınaq təhvil verilir', paidResult.status === 200, `status=${paidResult.status}`);
  check('Ümumi nəticə 50%', paidResult.body?.scorePercent === 50,
    `score=${paidResult.body?.scorePercent}`);
  check('Keçid balından aşağı olduğu üçün keçmir', paidResult.body?.passed === false);
  check('Keçmədiyi üçün sertifikat verilmir',
    paidResult.body?.certificateCode === null || paidResult.body?.certificateCode === undefined,
    `code=${paidResult.body?.certificateCode}`);

  const mathResult = paidResult.body?.sections?.find(s => s.subject === 'Riyaziyyat');
  const physicsResult = paidResult.body?.sections?.find(s => s.subject === 'Fizika');

  check('Riyaziyyat bölməsi 100%', mathResult?.scorePercent === 100,
    `score=${mathResult?.scorePercent}`);
  check('Fizika bölməsi 0%', physicsResult?.scorePercent === 0,
    `score=${physicsResult?.scorePercent}`);
  check('Fənn üzrə düzgün cavab sayı doğrudur',
    mathResult?.correctCount === 2 && physicsResult?.correctCount === 0);

  const resultAgain = await call('GET', `/exams/attempts/${paidRun.body.attemptId}`, { token: student });
  check('Nəticə sonradan yenidən oxunur', resultAgain.status === 200);
  check('Başqası nəticəni görə bilmir (403)',
    (await call('GET', `/exams/attempts/${paidRun.body.attemptId}`, { token: tural })).status === 403);

  // ------------------------------------------------- 4. Müəllim: CRUD və moderasiya
  section('4. Müəllim sınaq yaradır → moderasiya');

  const createdExam = await call('POST', '/exams', {
    token: nigar,
    body: {
      name: 'Kimya — atom quruluşu sınağı',
      description: 'Atom quruluşu üzrə qısa sınaq.',
      subject: 'Kimya',
      grade: 9,
      durationMinutes: 20,
      passPercent: 70,
      isPaid: true,
      price: 4.5,
      sections: [{
        subject: 'Kimya',
        questions: [
          {
            questionText: 'Atomun mərkəzində nə yerləşir?',
            options: ['Elektron', 'Nüvə', 'Molekul', 'İon'],
            correctOptionIndex: 1,
          },
          {
            questionText: 'Protonun yükü necədir?',
            options: ['Mənfi', 'Neytral', 'Müsbət'],
            correctOptionIndex: 2,
          },
        ],
      }],
    },
  });

  check('Müəllim sınaq yaradır (201)', createdExam.status === 201,
    `status=${createdExam.status} ${(createdExam.text ?? '').slice(0, 160)}`);
  check('Yeni sınaq Pending statusundadır', createdExam.body?.status === 'Pending');
  const newExamId = createdExam.body?.id;

  const publicList = await call('GET', '/exams');
  check('Pending sınaq ictimai kataloqda yoxdur',
    !publicList.body?.items?.some(e => e.id === newExamId));

  const mineList = await call('GET', '/exams/mine', { token: nigar });
  check('Müəllif öz Pending sınağını görür',
    mineList.body?.some(e => e.id === newExamId));

  const studentCreate = await call('POST', '/exams', {
    token: student,
    body: {
      name: 'İcazəsiz sınaq', description: 'X', subject: 'X',
      durationMinutes: 10, passPercent: 50, isPaid: false, price: 0,
      sections: [{ subject: 'X', questions: [{ questionText: 'Y', options: ['a', 'b'], correctOptionIndex: 0 }] }],
    },
  });
  check('Şagird sınaq yarada bilmir (403)', studentCreate.status === 403,
    `status=${studentCreate.status}`);

  const noQuestions = await call('POST', '/exams', {
    token: nigar,
    body: {
      name: 'Boş sınaq', description: 'X', subject: 'X',
      durationMinutes: 10, passPercent: 50, isPaid: false, price: 0,
      sections: [],
    },
  });
  check('Bölməsiz sınaq 400 verir', noQuestions.status === 400, `status=${noQuestions.status}`);

  const badIndex = await call('POST', '/exams', {
    token: nigar,
    body: {
      name: 'Səhv indeks', description: 'X', subject: 'X',
      durationMinutes: 10, passPercent: 50, isPaid: false, price: 0,
      sections: [{ subject: 'X', questions: [{ questionText: 'Y', options: ['a', 'b'], correctOptionIndex: 5 }] }],
    },
  });
  check('Sərhəddən kənar correctOptionIndex 400 verir', badIndex.status === 400,
    `status=${badIndex.status}`);

  const pendingQueue = await call('GET', '/admin/exams/pending', { token: admin });
  check('Moderasiya növbəsində görünür',
    pendingQueue.body?.some(e => e.id === newExamId));

  const queueRow = pendingQueue.body?.find(e => e.id === newExamId);
  check('Moderator bölmə və sual sayını görür', queueRow?.questionCount === 2,
    `count=${queueRow?.questionCount}`);
  check('Moderator müəllif adını görür', Boolean(queueRow?.authorName));

  const teacherQueue = await call('GET', '/admin/exams/pending', { token: nigar });
  check('Müəllim moderasiya növbəsinə girə bilmir (403)', teacherQueue.status === 403);

  const rejected = await call('POST', `/admin/exams/${newExamId}/reject`, {
    token: admin,
    body: { reason: 'Sual sayı azdır' },
  });
  check('Admin sınağı rədd edir', rejected.body?.status === 'Rejected');
  check('Rədd səbəbi saxlanılır', rejected.body?.rejectionReason === 'Sual sayı azdır');

  const fixedExam = await call('PUT', `/exams/${newExamId}`, {
    token: nigar,
    body: {
      name: 'Kimya — atom quruluşu sınağı (düzəliş)',
      description: 'Atom quruluşu üzrə sınaq.',
      subject: 'Kimya',
      grade: 9,
      durationMinutes: 25,
      passPercent: 70,
      isPaid: true,
      price: 4.5,
    },
  });
  check('Rədd edilmiş sınaq redaktədən sonra yenidən Pending olur',
    fixedExam.body?.status === 'Pending', `status=${fixedExam.body?.status}`);

  const approved = await call('POST', `/admin/exams/${newExamId}/approve`, { token: admin });
  check('Admin sınağı təsdiqləyir', approved.body?.status === 'Approved');
  check('approvedAt doldurulur', Boolean(approved.body?.approvedAt));

  const publicAfter = await call('GET', '/exams');
  check('Təsdiqlənmiş sınaq kataloqda görünür',
    publicAfter.body?.items?.some(e => e.id === newExamId));

  // ------------------------------------------------- 5. İcazələr və silmə
  section('5. Sahiblik, redaktə və silmə qaydaları');

  const foreignUpdate = await call('PUT', `/exams/${newExamId}`, {
    token: elvin,
    body: {
      name: 'Oğurlanmış sınaq', description: 'X', subject: 'X',
      durationMinutes: 10, passPercent: 50, isPaid: false, price: 0,
    },
  });
  check('Başqa müəllim redaktə edə bilmir (403)', foreignUpdate.status === 403,
    `status=${foreignUpdate.status}`);

  const ownerStart = await call('POST', `/exams/${newExamId}/start`, { token: nigar });
  check('Müəllif öz sınağını verə bilmir (400)', ownerStart.status === 400,
    `status=${ownerStart.status}`);

  const replaceSections = await call('PUT', `/exams/${newExamId}/sections`, {
    token: nigar,
    body: {
      sections: [{
        subject: 'Kimya',
        questions: [
          { questionText: 'Su molekulunun formulu?', options: ['H2O', 'CO2', 'O2'], correctOptionIndex: 0 },
          { questionText: 'Duzun formulu?', options: ['NaCl', 'HCl', 'KOH'], correctOptionIndex: 0 },
          { questionText: 'Oksigenin işarəsi?', options: ['Os', 'O', 'Ox'], correctOptionIndex: 1 },
        ],
      }],
    },
  });
  check('Suallar əvəz olunur', replaceSections.status === 200, `status=${replaceSections.status}`);
  check('Yeni sual sayı 3-dür', replaceSections.body?.questionCount === 3);
  check('Məzmun dəyişdiyi üçün yenidən moderasiyaya düşür',
    replaceSections.body?.status === 'Pending', `status=${replaceSections.body?.status}`);

  await call('POST', `/admin/exams/${newExamId}/approve`, { token: admin });

  // Kimsə sınağı verəndən sonra sual dəyişmək və silmək olmaz.
  await call('POST', '/cart/items', {
    token: tural,
    body: { itemType: 'Exam', itemId: newExamId },
  });
  await call('POST', '/orders/checkout', { token: tural });
  const turalRun = await call('POST', `/exams/${newExamId}/start`, { token: tural });
  check('İkinci şagird sınağa başlayır', turalRun.status === 200, `status=${turalRun.status}`);

  const lockedSections = await call('PUT', `/exams/${newExamId}/sections`, {
    token: nigar,
    body: { sections: [{ subject: 'Kimya', questions: [{ questionText: 'Z', options: ['a', 'b'], correctOptionIndex: 0 }] }] },
  });
  check('Cəhd olan sınağın sualları dəyişmir (409)', lockedSections.status === 409,
    `status=${lockedSections.status}`);

  const lockedDelete = await call('DELETE', `/exams/${newExamId}`, { token: nigar });
  check('Cəhd olan sınaq silinmir (409)', lockedDelete.status === 409, `status=${lockedDelete.status}`);

  // Cəhdi olmayan yeni sınaq silinə bilər.
  const throwaway = await call('POST', '/exams', {
    token: nigar,
    body: {
      name: 'Silinəcək sınaq', description: 'X', subject: 'Kimya',
      durationMinutes: 10, passPercent: 50, isPaid: false, price: 0,
      sections: [{ subject: 'Kimya', questions: [{ questionText: 'Y', options: ['a', 'b'], correctOptionIndex: 0 }] }],
    },
  });
  const deleted = await call('DELETE', `/exams/${throwaway.body.id}`, { token: nigar });
  check('Cəhdi olmayan sınaq silinir (204)', deleted.status === 204, `status=${deleted.status}`);
  check('Silinmiş sınaq 404 verir',
    (await call('GET', `/exams/${throwaway.body.id}`)).status === 404);

  // ------------------------------------------------- 6. Admin icmalı
  section('6. Admin icmalında sınaq göstəriciləri');

  await new Promise(resolve => setTimeout(resolve, 6000));

  const dash = await call('GET', '/admin/dashboard', { token: admin });
  check('İcmal 200 qaytarır', dash.status === 200, `status=${dash.status}`);

  const exams = dash.body?.exams;
  check('Sınaq sayı doludur', exams?.totalExams >= 3, `total=${exams?.totalExams}`);
  check('Təsdiqlənmiş sınaq sayılır', exams?.approvedExams >= 3);
  check('Sual sayı doludur', exams?.totalQuestions > 0);
  check('Cəhd sayı doludur', exams?.totalAttempts === 3, `attempts=${exams?.totalAttempts}`);
  check('Davam edən cəhd sayılır', exams?.attemptsInProgress === 1,
    `inProgress=${exams?.attemptsInProgress}`);
  check('Keçən cəhd sayılır', exams?.passedAttempts === 1);
  check('Orta bal hesablanır', exams?.averageScorePercent > 0,
    `avg=${exams?.averageScorePercent}`);
  check('Satılan sınaq sayılır', exams?.soldExams === 2, `sold=${exams?.soldExams}`);
  // Satılan sınaqlar: 9.90 (buraxılış) + 4.50 (kimya) = 14.40
  check('Sınaq gəliri düzgün hesablanır', exams?.examRevenue === 14.40,
    `revenue=${exams?.examRevenue}`);

  const sales = await call('GET', '/admin/sales', { token: admin });
  check('Sınaqdan 20% komissiya tutulur',
    sales.body?.commissionTotal >= 2.88,
    `commission=${sales.body?.commissionTotal}`);

  console.log('\n==================================================');
  console.log(`NƏTİCƏ:  ${passed} keçdi, ${failed} uğursuz`);
  console.log('==================================================');

  process.exit(failed > 0 ? 1 : 0);
}

run().catch(err => {
  console.error('Test skripti xəta ilə dayandı:', err);
  process.exit(1);
});
