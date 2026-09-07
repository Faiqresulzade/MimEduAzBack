using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Domain.Constants;
using MimeduAz.Domain.Entities;
using MimeduAz.Domain.Enums;
using MimeduAz.Infrastructure.Storage;

namespace MimeduAz.Infrastructure.Persistence;

/// <summary>
/// Development mühiti üçün nümunə data. Sabit Guid-lər sayəsində təkrar işə salınanda
/// dublikat yaratmır - mövcud sətirlər olduğu kimi saxlanılır.
/// </summary>
public sealed class DataSeeder
{
    public const string AdminPassword = "Admin123!";
    public const string TeacherPassword = "Teacher123!";
    public const string StudentPassword = "Student123!";

    private static readonly DateTime Base = new(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc);

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<ApplicationRole> _roles;
    private readonly IFileStorageService _files;
    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users,
        RoleManager<ApplicationRole> roles,
        IFileStorageService files,
        ILogger<DataSeeder> logger)
    {
        _db = db;
        _users = users;
        _roles = roles;
        _files = files;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedRolesAsync();
        var teachers = await SeedUsersAsync();
        await SeedResourcesAsync(teachers, ct);
        await SeedTrainingsAsync(ct);
        await SeedTrainingLessonsAsync(ct);
        await SeedQuizzesAsync(ct);
        await SeedCertificatesAsync(ct);
        await SeedBlogAsync(ct);

        _logger.LogInformation("Seed data hazırdır.");
    }

    private async Task SeedRolesAsync()
    {
        foreach (var role in AppRoles.All)
        {
            if (!await _roles.RoleExistsAsync(role))
            {
                await _roles.CreateAsync(new ApplicationRole(role));
            }
        }
    }

    private async Task<Dictionary<string, ApplicationUser>> SeedUsersAsync()
    {
        var seeds = new[]
        {
            (Id: Ids.Admin,   Name: "Platforma Admini", Email: AppDefaults.AdminEmail, Subject: (string?)null, Role: AppRoles.Admin,   Password: AdminPassword),
            (Id: Ids.Nigar,   Name: "Nigar Əliyeva",    Email: "nigar@mimedu.az",      Subject: "Riyaziyyat",  Role: AppRoles.Teacher, Password: TeacherPassword),
            (Id: Ids.Elvin,   Name: "Elvin Məmmədov",   Email: "elvin@mimedu.az",      Subject: "Fizika",      Role: AppRoles.Teacher, Password: TeacherPassword),
            (Id: Ids.Aysel,   Name: "Aysel Hüseynova",  Email: "aysel@mimedu.az",      Subject: "Az. dili",    Role: AppRoles.Teacher, Password: TeacherPassword),
            (Id: Ids.Rashad,  Name: "Rəşad Quliyev",    Email: "reshad@mimedu.az",     Subject: "Biologiya",   Role: AppRoles.Teacher, Password: TeacherPassword),
            (Id: Ids.Leyla,   Name: "Leyla Səfərova",   Email: "leyla@mimedu.az",      Subject: "İngilis dili",Role: AppRoles.Teacher, Password: TeacherPassword),
            (Id: Ids.Kamran,  Name: "Kamran Abbasov",   Email: "kamran@mimedu.az",     Subject: "Tarix",       Role: AppRoles.Teacher, Password: TeacherPassword),
            // Yalnız təlim alan/keçən hesablar - material yükləyə bilmirlər.
            (Id: Ids.Sevinc,  Name: "Sevinc Orucova",   Email: "sevinc@mimedu.az",     Subject: "Kimya",       Role: AppRoles.Student, Password: StudentPassword),
            (Id: Ids.Tural,   Name: "Tural Hacıyev",    Email: "tural@mimedu.az",      Subject: (string?)null, Role: AppRoles.Student, Password: StudentPassword)
        };

        var result = new Dictionary<string, ApplicationUser>();

        foreach (var seed in seeds)
        {
            var user = await _users.FindByEmailAsync(seed.Email);

            if (user is null)
            {
                user = new ApplicationUser
                {
                    Id = seed.Id,
                    UserName = seed.Email,
                    Email = seed.Email,
                    EmailConfirmed = true,
                    FullName = seed.Name,
                    Subject = seed.Subject,
                    CreatedAt = Base
                };

                var created = await _users.CreateAsync(user, seed.Password);
                if (!created.Succeeded)
                {
                    var errors = string.Join("; ", created.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"Seed istifadəçisi yaradıla bilmədi ({seed.Email}): {errors}");
                }
            }

            if (!await _users.IsInRoleAsync(user, seed.Role))
            {
                await _users.AddToRoleAsync(user, seed.Role);
            }

            result[seed.Email] = user;
        }

        return result;
    }

    private async Task SeedResourcesAsync(Dictionary<string, ApplicationUser> users, CancellationToken ct)
    {
        if (await _db.Resources.AnyAsync(ct))
        {
            return;
        }

        var resources = new List<Resource>
        {
            new()
            {
                Id = Ids.ResFractions,
                Name = "Kəsrlər üzrə iş vərəqi (interaktiv tapşırıqlarla)",
                Subject = "Riyaziyyat",
                Grade = 5,
                Type = ResourceType.WorkSheet,
                AuthorId = users["nigar@mimedu.az"].Id,
                Downloads = 412,
                IsPaid = false,
                Price = 0m,
                Status = ResourceStatus.Approved,
                CreatedAt = Base.AddDays(3),
                ApprovedAt = Base.AddDays(4)
            },
            new()
            {
                Id = Ids.ResGeometry,
                Name = "Həndəsi fiqurlar — təqdimat dəsti",
                Subject = "Riyaziyyat",
                Grade = 7,
                Type = ResourceType.Presentation,
                AuthorId = users["nigar@mimedu.az"].Id,
                Downloads = 268,
                IsPaid = true,
                Price = 6.00m,
                Status = ResourceStatus.Approved,
                CreatedAt = Base.AddDays(9),
                ApprovedAt = Base.AddDays(10)
            },
            new()
            {
                Id = Ids.ResPhysics,
                Name = "Mexanika: sürət və təcil üzrə test bankı",
                Subject = "Fizika",
                Grade = 9,
                Type = ResourceType.Test,
                AuthorId = users["elvin@mimedu.az"].Id,
                Downloads = 197,
                IsPaid = true,
                Price = 8.50m,
                Status = ResourceStatus.Approved,
                CreatedAt = Base.AddDays(14),
                ApprovedAt = Base.AddDays(15)
            },
            new()
            {
                Id = Ids.ResAzLang,
                Name = "Mətn üzərində iş — metodik vəsait",
                Subject = "Az. dili",
                Grade = 6,
                Type = ResourceType.MethodGuide,
                AuthorId = users["aysel@mimedu.az"].Id,
                Downloads = 331,
                IsPaid = false,
                Price = 0m,
                Status = ResourceStatus.Approved,
                CreatedAt = Base.AddDays(18),
                ApprovedAt = Base.AddDays(19)
            },
            new()
            {
                Id = Ids.ResBiology,
                Name = "Hüceyrə quruluşu — laboratoriya iş vərəqi",
                Subject = "Biologiya",
                Grade = 8,
                Type = ResourceType.WorkSheet,
                AuthorId = users["reshad@mimedu.az"].Id,
                Downloads = 154,
                IsPaid = false,
                Price = 0m,
                Status = ResourceStatus.Approved,
                CreatedAt = Base.AddDays(22),
                ApprovedAt = Base.AddDays(23)
            },
            new()
            {
                Id = Ids.ResEnglish,
                Name = "Present Perfect — qrammatika təqdimatı və tapşırıqlar",
                Subject = "İngilis dili",
                Grade = 10,
                Type = ResourceType.Presentation,
                AuthorId = users["leyla@mimedu.az"].Id,
                Downloads = 0,
                IsPaid = true,
                Price = 5.00m,
                // Moderasiya növbəsini nümayiş etdirmək üçün təsdiqlənməmiş saxlanılır.
                Status = ResourceStatus.Pending,
                CreatedAt = Base.AddDays(27)
            }
        };

        // Hər seed resursu üçün nümunə PDF yazılır ki, endirmə axını
        // development mühitində uçdan-uca işlək olsun.
        foreach (var resource in resources)
        {
            var bytes = PlaceholderPdf.Create(resource.Name);
            var fileName = $"{Slugify(resource.Name)}.pdf";

            using var stream = new MemoryStream(bytes);
            resource.FilePath = await _files.SaveAsync(stream, fileName, ct);
            resource.OriginalFileName = fileName;
        }

        _db.Resources.AddRange(resources);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Resursun adından fayl adı üçün yararlı ASCII slug qurur.</summary>
    private static string Slugify(string name)
    {
        var map = new Dictionary<char, char>
        {
            ['ə'] = 'e', ['ı'] = 'i', ['ö'] = 'o', ['ü'] = 'u',
            ['ğ'] = 'g', ['ş'] = 's', ['ç'] = 'c', ['İ'] = 'i'
        };

        var chars = name.ToLowerInvariant()
            .Select(c => map.TryGetValue(c, out var replacement) ? replacement : c)
            .Select(c => char.IsLetterOrDigit(c) && c <= 'z' ? c : '-')
            .ToArray();

        var slug = new string(chars).Trim('-');
        while (slug.Contains("--"))
        {
            slug = slug.Replace("--", "-");
        }

        return slug.Length > 60 ? slug[..60].Trim('-') : slug;
    }

    private async Task SeedTrainingsAsync(CancellationToken ct)
    {
        if (await _db.Trainings.AnyAsync(ct))
        {
            return;
        }

        var trainings = new List<Training>
        {
            new()
            {
                Id = Ids.TrainAi,
                Name = "Süni intellektlə dərs dizaynı",
                Format = TrainingFormat.Live,
                Description = "Müəllimlər üçün praktik emalatxana: AI alətləri ilə dərs planı, tapşırıq və qiymətləndirmə materiallarının hazırlanması.",
                Price = 120.00m,
                DurationHours = 8,
                MetaLabel = "2 gün · 8 saat",
                SeatLimit = 25,
                CreatedAt = Base,
                SyllabusItems =
                {
                    new TrainingSyllabusItem { OrderIndex = 0, Text = "AI alətlərinə giriş və təhsildə etik istifadə" },
                    new TrainingSyllabusItem { OrderIndex = 1, Text = "Dərs planının prompt ilə hazırlanması" },
                    new TrainingSyllabusItem { OrderIndex = 2, Text = "Fərqləndirilmiş tapşırıqların yaradılması" },
                    new TrainingSyllabusItem { OrderIndex = 3, Text = "Qiymətləndirmə rubrikası və geribildirim" }
                }
            },
            new()
            {
                Id = Ids.TrainAssessment,
                Name = "Formativ qiymətləndirmə praktikası",
                Format = TrainingFormat.Online,
                Description = "Onlayn sessiyalarla formativ qiymətləndirmə alətlərinin sinifdə tətbiqi.",
                Price = 75.00m,
                DurationHours = 12,
                MetaLabel = "6 sessiya",
                SeatLimit = null,
                CreatedAt = Base.AddDays(5),
                SyllabusItems =
                {
                    new TrainingSyllabusItem { OrderIndex = 0, Text = "Formativ və summativ qiymətləndirmənin fərqi" },
                    new TrainingSyllabusItem { OrderIndex = 1, Text = "Sinifdaxili sürətli qiymətləndirmə texnikaları" },
                    new TrainingSyllabusItem { OrderIndex = 2, Text = "Rubrika hazırlanması" },
                    new TrainingSyllabusItem { OrderIndex = 3, Text = "Şagird portfoliosu ilə iş" },
                    new TrainingSyllabusItem { OrderIndex = 4, Text = "Valideynlə nəticələrin paylaşılması" },
                    new TrainingSyllabusItem { OrderIndex = 5, Text = "Yekun layihə təqdimatı" }
                }
            },
            new()
            {
                Id = Ids.TrainClassroom,
                Name = "Sinif idarəetməsi: video kurs",
                Format = TrainingFormat.Video,
                Description = "Öz sürətinizlə keçə biləcəyiniz video kurs: davranış idarəetməsi, motivasiya və sinif mühiti.",
                Price = 45.00m,
                DurationHours = 6,
                MetaLabel = "18 dərs",
                SeatLimit = null,
                CreatedAt = Base.AddDays(11),
                SyllabusItems =
                {
                    new TrainingSyllabusItem { OrderIndex = 0, Text = "İlk həftədə sinif qaydalarının qurulması" },
                    new TrainingSyllabusItem { OrderIndex = 1, Text = "Diqqəti cəlb etmə texnikaları" },
                    new TrainingSyllabusItem { OrderIndex = 2, Text = "Çətin davranışlarla iş" },
                    new TrainingSyllabusItem { OrderIndex = 3, Text = "Motivasiya və mükafatlandırma sistemi" }
                }
            }
        };

        _db.Trainings.AddRange(trainings);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Təlimlərin dərslərini doldurur. Təlim-təlim yoxlanılır ki, artıq mövcud olan
    /// (məs. əvvəlki versiyada yaradılmış) təlimlərə də dərslər əlavə olunsun.
    /// </summary>
    private async Task SeedTrainingLessonsAsync(CancellationToken ct)
    {
        var lessonsByTraining = new Dictionary<Guid, List<(string Title, string Description, string VideoUrl, int Minutes)>>
        {
            [Ids.TrainAi] = new()
            {
                ("AI alətlərinə giriş və təhsildə etik istifadə",
                 "Süni intellektin sinifdə hansı məsələləri həll etdiyi, hansılarını isə həll etmədiyi. Şagird məlumatlarının qorunması və etik sərhədlər.",
                 "https://www.youtube.com/watch?v=example-ai-intro", 45),
                ("Dərs planının prompt ilə hazırlanması",
                 "Konkret nəticə təsvir edən prompt yazmaq: sinif səviyyəsi, məqsəd və çətinlik dərəcəsi. Nümunələr üzərində praktika.",
                 "https://www.youtube.com/watch?v=example-lesson-plan", 60),
                ("Fərqləndirilmiş tapşırıqların yaradılması",
                 "Eyni mövzu üzrə üç fərqli səviyyədə tapşırıq dəsti hazırlamaq və onları sinifdə tətbiq etmək.",
                 "https://www.youtube.com/watch?v=example-differentiation", 55),
                ("Qiymətləndirmə rubrikası və geribildirim",
                 "AI ilə rubrika qurmaq və şagirdə fərdi geribildirim hazırlamaq. Nəticələrin yoxlanılması müəllimin məsuliyyətindədir.",
                 "https://www.youtube.com/watch?v=example-rubric", 50)
            },
            [Ids.TrainAssessment] = new()
            {
                ("Formativ və summativ qiymətləndirmənin fərqi",
                 "Nə vaxt qiymət qoyulur, nə vaxt isə dərsin gedişatı dəyişdirilir. Praktik nümunələr.",
                 "https://www.youtube.com/watch?v=example-formative-1", 40),
                ("Sinifdaxili sürətli qiymətləndirmə texnikaları",
                 "Çıxış vərəqi, barmaqla özünüqiymətləndirmə, sürətli sorğu — əlavə resurs tələb etməyən beş texnika.",
                 "https://www.youtube.com/watch?v=example-formative-2", 45),
                ("Rubrika hazırlanması",
                 "Şagirdin başa düşəcəyi dildə meyar yazmaq və rubrikanı dərsdən əvvəl paylaşmaq.",
                 "https://www.youtube.com/watch?v=example-formative-3", 50),
                ("Şagird portfoliosu ilə iş",
                 "İrəliləyişi zaman içində göstərən portfolio qurmaq və onu qiymətləndirmədə istifadə etmək.",
                 "https://www.youtube.com/watch?v=example-formative-4", 45),
                ("Valideynlə nəticələrin paylaşılması",
                 "Qiymət yox, irəliləyiş danışan valideyn görüşü: nümunə strukturu və ifadələr.",
                 "https://www.youtube.com/watch?v=example-formative-5", 35),
                ("Yekun layihə təqdimatı",
                 "Öz sinfiniz üçün hazırladığınız qiymətləndirmə planının təqdimatı və müzakirəsi.",
                 "https://www.youtube.com/watch?v=example-formative-6", 45)
            },
            [Ids.TrainClassroom] = new()
            {
                ("İlk həftədə sinif qaydalarının qurulması",
                 "Qaydaları şagirdlərlə birlikdə yazmaq və ilk həftədə möhkəmləndirmək.",
                 "https://www.youtube.com/watch?v=example-class-1", 30),
                ("Diqqəti cəlb etmə texnikaları",
                 "Səsi qaldırmadan sinfin diqqətini toplamağın praktik üsulları.",
                 "https://www.youtube.com/watch?v=example-class-2", 25),
                ("Çətin davranışlarla iş",
                 "Davranışın səbəbini anlamaq və münaqişəni böyütmədən idarə etmək.",
                 "https://www.youtube.com/watch?v=example-class-3", 40),
                ("Motivasiya və mükafatlandırma sistemi",
                 "Daxili motivasiyanı zədələməyən mükafat sistemi qurmaq.",
                 "https://www.youtube.com/watch?v=example-class-4", 35)
            }
        };

        var added = 0;

        foreach (var (trainingId, lessons) in lessonsByTraining)
        {
            var trainingExists = await _db.Trainings.AnyAsync(t => t.Id == trainingId, ct);
            var alreadyHasLessons = await _db.TrainingLessons.AnyAsync(l => l.TrainingId == trainingId, ct);

            if (!trainingExists || alreadyHasLessons)
            {
                continue;
            }

            var order = 0;
            foreach (var (title, description, videoUrl, minutes) in lessons)
            {
                _db.TrainingLessons.Add(new TrainingLesson
                {
                    TrainingId = trainingId,
                    OrderIndex = order++,
                    Title = title,
                    Description = description,
                    VideoUrl = videoUrl,
                    DurationMinutes = minutes,
                    CreatedAt = Base
                });
                added++;
            }
        }

        if (added > 0)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("{Count} təlim dərsi seed edildi.", added);
        }
    }

    private async Task SeedQuizzesAsync(CancellationToken ct)
    {
        if (await _db.Quizzes.AnyAsync(ct))
        {
            return;
        }

        var fractionsQuiz = new Quiz
        {
            Id = Ids.QuizFractions,
            ResourceId = Ids.ResFractions,
            PassPercent = 70,
            CreatedAt = Base.AddDays(5),
            Questions =
            {
                new QuizQuestion
                {
                    OrderIndex = 0,
                    QuestionText = "1/2 + 1/4 neçəyə bərabərdir?",
                    Options = new List<string> { "2/6", "3/4", "1/6", "2/4" },
                    CorrectOptionIndex = 1
                },
                new QuizQuestion
                {
                    OrderIndex = 1,
                    QuestionText = "Hansı kəsr düzgün kəsrdir?",
                    Options = new List<string> { "7/5", "9/9", "3/8", "11/4" },
                    CorrectOptionIndex = 2
                },
                new QuizQuestion
                {
                    OrderIndex = 2,
                    QuestionText = "3/6 kəsrinin ixtisar olunmuş forması hansıdır?",
                    Options = new List<string> { "1/2", "1/3", "2/3", "3/2" },
                    CorrectOptionIndex = 0
                },
                new QuizQuestion
                {
                    OrderIndex = 3,
                    QuestionText = "0,25 onluq kəsri adi kəsrlə necə yazılır?",
                    Options = new List<string> { "1/5", "1/4", "2/5", "1/3" },
                    CorrectOptionIndex = 1
                }
            }
        };

        var physicsQuiz = new Quiz
        {
            Id = Ids.QuizPhysics,
            ResourceId = Ids.ResPhysics,
            PassPercent = 75,
            CreatedAt = Base.AddDays(16),
            Questions =
            {
                new QuizQuestion
                {
                    OrderIndex = 0,
                    QuestionText = "Sürətin SI sistemində ölçü vahidi hansıdır?",
                    Options = new List<string> { "m/s", "m/s²", "N", "J" },
                    CorrectOptionIndex = 0
                },
                new QuizQuestion
                {
                    OrderIndex = 1,
                    QuestionText = "Bərabərsürətli hərəkətdə təcil neçəyə bərabərdir?",
                    Options = new List<string> { "1", "Sürətə bərabərdir", "0", "Yola bərabərdir" },
                    CorrectOptionIndex = 2
                },
                new QuizQuestion
                {
                    OrderIndex = 2,
                    QuestionText = "60 km/h sürət neçə m/s-dir?",
                    Options = new List<string> { "16,7 m/s", "6 m/s", "600 m/s", "36 m/s" },
                    CorrectOptionIndex = 0
                }
            }
        };

        _db.Quizzes.AddRange(fractionsQuiz, physicsQuiz);
        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedCertificatesAsync(CancellationToken ct)
    {
        if (await _db.Certificates.AnyAsync(ct))
        {
            return;
        }

        var issuedAt = new DateTime(2026, 3, 14, 12, 0, 0, DateTimeKind.Utc);

        // Təlimə yazılma + tamamlanmış status: sertifikatın arxa planını real edir.
        _db.Enrollments.Add(new Enrollment
        {
            Id = Ids.EnrollNigarAi,
            UserId = Ids.Nigar,
            TrainingId = Ids.TrainAi,
            ProgressPercent = 100,
            Status = EnrollmentStatus.Completed,
            EnrolledAt = Base.AddDays(20),
            CompletedAt = issuedAt
        });

        // 100% faiz dərs tamamlamaları ilə uzlaşsın deyə hər dərs üçün qeyd yazılır.
        var aiLessons = await _db.TrainingLessons
            .Where(l => l.TrainingId == Ids.TrainAi)
            .OrderBy(l => l.OrderIndex)
            .ToListAsync(ct);

        foreach (var lesson in aiLessons)
        {
            _db.LessonCompletions.Add(new LessonCompletion
            {
                EnrollmentId = Ids.EnrollNigarAi,
                LessonId = lesson.Id,
                CompletedAt = Base.AddDays(20 + lesson.OrderIndex)
            });
        }

        _db.Certificates.Add(new Certificate
        {
            Id = Ids.CertNigar,
            // Doğrulama axınını yoxlamaq üçün sabit test kodu.
            Code = "MIM-2026-4417",
            UserId = Ids.Nigar,
            TrainingId = Ids.TrainAi,
            Description = "Nigar Əliyeva · «Süni intellektlə dərs dizaynı» · 8 saat · 14.03.2026",
            IssuedAt = issuedAt
        });

        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedBlogAsync(CancellationToken ct)
    {
        if (await _db.BlogPosts.AnyAsync(ct))
        {
            return;
        }

        _db.BlogPosts.AddRange(
            new BlogPost
            {
                Id = Ids.BlogAi,
                Title = "Süni intellekt dərsə necə daxil edilir?",
                Tag = "Rəqəmsal təhsil",
                ReadTime = "6 dəq",
                Excerpt = "AI alətlərini dərs planına inteqrasiya etməyin praktik yolları və qaçınılmalı səhvlər.",
                Body = new List<string>
                {
                    "Süni intellekt alətləri müəllimin işini əvəz etmir, onu sürətləndirir. Ən böyük fayda hazırlıq mərhələsindədir: dərs planının strukturu, tapşırıq variantları və qiymətləndirmə meyarları dəqiqələr içində hazırlana bilir.",
                    "İlk addım konkret nəticəni təsvir etməkdir. «5-ci sinif üçün kəsrlər mövzusunda 10 tapşırıq» kimi ümumi sorğu yerinə şagirdlərin səviyyəsini, dərsin məqsədini və gözlənilən çətinlik dərəcəsini göstərin.",
                    "Nəhayət, hər nəticəni yoxlayın. AI-nin hazırladığı material metodiki cəhətdən düzgün görünsə də, kurikuluma uyğunluğu yalnız müəllim təsdiqləyə bilər."
                },
                CreatedAt = Base.AddDays(6)
            },
            new BlogPost
            {
                Id = Ids.BlogAssessment,
                Title = "Formativ qiymətləndirmə: 5 sadə texnika",
                Tag = "Qiymətləndirmə",
                ReadTime = "4 dəq",
                Excerpt = "Dərsin sonunda deyil, dərsin içində şagirdin nəyi anladığını görməyin yolları.",
                Body = new List<string>
                {
                    "Formativ qiymətləndirmənin məqsədi qiymət qoymaq deyil, dərsin gedişatını dəyişməkdir. Buna görə də nəticələri jurnal üçün deyil, növbəti 10 dəqiqə üçün istifadə edin.",
                    "Çıxış vərəqi, barmaqla özünüqiymətləndirmə, sürətli sorğu, cüt-cüt izahat və səhv tapma tapşırığı — bu beş texnika heç bir əlavə resurs tələb etmir.",
                    "Vacib şərt: topladığınız məlumata reaksiya verin. Əks halda texnika ritual halına gəlir və şagirdlər onu ciddi qəbul etmir."
                },
                CreatedAt = Base.AddDays(13)
            },
            new BlogPost
            {
                Id = Ids.BlogResources,
                Title = "Öz materialınızı paylaşmağa necə başlamalı?",
                Tag = "Resurs Bankı",
                ReadTime = "5 dəq",
                Excerpt = "Hazırladığınız iş vərəqlərini digər müəllimlərlə paylaşmaq üçün praktik bələdçi.",
                Body = new List<string>
                {
                    "Ən yaxşı material sizin artıq sinifdə sınaqdan keçirdiyiniz materialdır. Yeni bir şey hazırlamağa ehtiyac yoxdur — mövcud fayllarınızı səliqəyə salmaqla başlayın.",
                    "Fayl adı, fənn, sinif və materialın növü aydın göstərilməlidir. Bu məlumatlar digər müəllimlərin axtarışda sizin materialınızı tapmasını təmin edir.",
                    "Yüklədiyiniz hər material moderasiyadan keçir. Təsdiqləndikdən sonra Resurs Bankında görünür və endirmə statistikanız profilinizdə toplanır."
                },
                CreatedAt = Base.AddDays(24)
            });

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Seed sətirlərinin sabit identifikatorları.</summary>
    private static class Ids
    {
        public static readonly Guid Admin = new("11111111-1111-1111-1111-111111111111");
        public static readonly Guid Nigar = new("22222222-2222-2222-2222-222222222221");
        public static readonly Guid Elvin = new("22222222-2222-2222-2222-222222222222");
        public static readonly Guid Aysel = new("22222222-2222-2222-2222-222222222223");
        public static readonly Guid Rashad = new("22222222-2222-2222-2222-222222222224");
        public static readonly Guid Leyla = new("22222222-2222-2222-2222-222222222225");
        public static readonly Guid Kamran = new("22222222-2222-2222-2222-222222222226");
        public static readonly Guid Sevinc = new("22222222-2222-2222-2222-222222222227");
        public static readonly Guid Tural = new("22222222-2222-2222-2222-222222222228");

        public static readonly Guid ResFractions = new("33333333-3333-3333-3333-333333333331");
        public static readonly Guid ResGeometry = new("33333333-3333-3333-3333-333333333332");
        public static readonly Guid ResPhysics = new("33333333-3333-3333-3333-333333333333");
        public static readonly Guid ResAzLang = new("33333333-3333-3333-3333-333333333334");
        public static readonly Guid ResBiology = new("33333333-3333-3333-3333-333333333335");
        public static readonly Guid ResEnglish = new("33333333-3333-3333-3333-333333333336");

        public static readonly Guid TrainAi = new("44444444-4444-4444-4444-444444444441");
        public static readonly Guid TrainAssessment = new("44444444-4444-4444-4444-444444444442");
        public static readonly Guid TrainClassroom = new("44444444-4444-4444-4444-444444444443");

        public static readonly Guid QuizFractions = new("55555555-5555-5555-5555-555555555551");
        public static readonly Guid QuizPhysics = new("55555555-5555-5555-5555-555555555552");

        public static readonly Guid EnrollNigarAi = new("66666666-6666-6666-6666-666666666661");
        public static readonly Guid CertNigar = new("77777777-7777-7777-7777-777777777771");

        public static readonly Guid BlogAi = new("88888888-8888-8888-8888-888888888881");
        public static readonly Guid BlogAssessment = new("88888888-8888-8888-8888-888888888882");
        public static readonly Guid BlogResources = new("88888888-8888-8888-8888-888888888883");
    }
}
