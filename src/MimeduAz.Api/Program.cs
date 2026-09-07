using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using MimeduAz.Api.Middleware;
using MimeduAz.Api.Services;
using MimeduAz.Api.Swagger;
using MimeduAz.Application;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Options;
using MimeduAz.Contracts.Common;
using MimeduAz.Infrastructure;
using MimeduAz.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext());

// wwwroot layihə şablonunda avtomatik yaradılmaya bilər - fayl yükləmə üçün mütləq lazımdır.
var webRootPath = builder.Environment.WebRootPath
    ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(webRootPath);

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration, webRootPath);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Enum-lar frontend üçün mətn kimi ("WorkSheet") serializasiya olunur.
        // Null sahələr qəsdən buraxılmır: TypeScript tərəfdə `null` ilə `undefined`
        // arasında fərq olmasın deyə (məs. limitsiz təlimdə seatLimit: null).
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddFluentValidationAutoValidation();

// Model validasiya xətaları da ApiErrorResponse formatında qaytarılır.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .ToDictionary(
                e => char.ToLowerInvariant(e.Key[0]) + e.Key[1..],
                e => e.Value!.Errors.Select(x => x.ErrorMessage).ToArray());

        return new BadRequestObjectResult(new ApiErrorResponse
        {
            StatusCode = StatusCodes.Status400BadRequest,
            Message = "Göndərilən məlumatlar düzgün deyil.",
            Errors = errors,
            TraceId = context.HttpContext.TraceIdentifier
        });
    };
});

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwt = jwtSection.Get<JwtOptions>()
    ?? throw new InvalidOperationException("\"Jwt\" konfiqurasiya bölməsi tapılmadı.");

if (string.IsNullOrWhiteSpace(jwt.SecretKey) || jwt.SecretKey.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:SecretKey ən azı 32 simvol olmalıdır. Dəyəri user-secrets və ya environment variable ilə verin.");
}

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

const string CorsPolicy = "MimeduFrontend";

// Origin-lər iki formatda verilə bilər:
//   Cors__AllowedOrigins__0=https://mimedu.az        (indeksli massiv)
//   Cors__AllowedOrigins=https://a.az,https://b.az   (vergüllə - hostinq panellərində daha rahat)
var corsSection = builder.Configuration.GetSection("Cors:AllowedOrigins");

var rawOrigins = corsSection.Value is { Length: > 0 } inlineValue
    ? new[] { inlineValue }
    : corsSection.Get<string[]>() ?? Array.Empty<string>();

// Konfiqurasiya panellərində dəyər çox vaxt səhv formatda yapışdırılır: dırnaq içində,
// nöqtəli vergüllə, sonunda "/" və ya artıq boşluqla. CORS müqayisəsi hərfi olduğu üçün
// belə dəyər səssizcə heç nəyə uyğun gəlmir və brauzerdə ümumi "CORS xətası" görünür.
// Ona görə bütün ayrıcıları qəbul edib dəyərləri normallaşdırırıq.
var allowedOrigins = rawOrigins
    .SelectMany(value => value.Split(
        new[] { ',', ';', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
    .Select(origin => origin.Trim().Trim('"', '\'').Trim().TrimEnd('/'))
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

if (allowedOrigins.Length == 0)
{
    allowedOrigins = new[] { "http://localhost:5173" };
}

// Formatı düzgün olmayan dəyər sükutla yox sayılmasın - loga düşsün.
var invalidOrigins = allowedOrigins
    .Where(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                     || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
    .ToArray();

builder.Services.AddCors(options =>
    options.AddPolicy(CorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddSwaggerWithJwt();

var app = builder.Build();

// Render/Heroku/Railway kimi platformalarda TLS bir kənar proksidə bitir və tətbiqə
// HTTP kimi çatır. Bu middleware olmadan UseHttpsRedirection sonsuz yönləndirmə
// döngüsü yaradır - proksi https-i "bilmir", həmişə http-dən https-ə yönləndirir.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
// Bulud proksisinin IP-si sabit deyil - default məhdudiyyəti təmizləyirik.
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

// Qalxarkən icazə verilən origin-ləri loglayırıq - CORS problemini diaqnoz etmək üçün
// ilk baxılacaq yer məhz budur (Render/hostinq loglarında dərhal görünür).
app.Logger.LogInformation(
    "CORS icazə verilən origin-lər ({Count}): {Origins}",
    allowedOrigins.Length, string.Join(" | ", allowedOrigins));

if (invalidOrigins.Length > 0)
{
    app.Logger.LogWarning(
        "CORS: bu dəyərlər düzgün origin deyil və heç bir sorğuya uyğun gəlməyəcək: {Invalid}. "
        + "Origin yalnız sxem+host(+port) olmalıdır, məs. https://mimedu.az (yol və sondakı \"/\" olmadan).",
        string.Join(" | ", invalidOrigins));
}

// İcazəsiz origin-dən gələn sorğunu xəbərdarlıq kimi loglayırıq. Brauzer belə halda
// yalnız ümumi "CORS xətası" göstərir - səbəbi yalnız server tərəfdə görünür.
app.Use(async (context, next) =>
{
    var origin = context.Request.Headers.Origin.ToString();

    if (!string.IsNullOrEmpty(origin)
        && !allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
    {
        app.Logger.LogWarning(
            "CORS: icazə verilməyən origin rədd edildi -> {Origin} (yol: {Path}). İcazəlilər: {Allowed}",
            origin, context.Request.Path, string.Join(", ", allowedOrigins));
    }

    await next();
});

// API cavabları keşlənməməlidir. Əks halda CORS başlığı olmadan alınmış köhnə cavab
// (məs. CORS konfiqurasiyasından əvvəl, ya da Origin başlığı olmayan sorğudan)
// brauzer/CDN keşində qalır və sonrakı cross-origin sorğularda təkrar istifadə olunub
// "CORS xətası" kimi görünür. Cavablar həm də istifadəçiyə xas olduğu üçün (JWT)
// heç bir halda paylaşılan keşdə saxlanılmamalıdır.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            context.Response.Headers.Pragma = "no-cache";

            // Keş yenə də saxlasa belə, cavabı Origin-ə görə ayırsın.
            // CORS middleware onsuz da əlavə edə bilir - təkrarlanmasın deyə yoxlayırıq.
            var vary = context.Response.Headers.Vary;
            if (!vary.Contains(HeaderNames.Origin, StringComparer.OrdinalIgnoreCase))
            {
                context.Response.Headers.Append(HeaderNames.Vary, HeaderNames.Origin);
            }

            return Task.CompletedTask;
        });
    }

    await next();
});

// Audit log ən kənarda dayanır ki, ExceptionHandlingMiddleware xətanı 401/404/409-a
// çevirdikdən SONRA yekun status kodunu və cavab gövdəsini görsün.
// İstifadəçi claim-ləri sorğu geri qayıdarkən HttpContext-də artıq mövcud olur.
app.UseRequestLogging();

app.UseExceptionHandling();
app.UseSerilogRequestLogging();

// Swagger defolt olaraq yalnız Development-də açıqdır. "Swagger:Enabled" konfiqurasiyası
// ilə mühiti dəyişmədən (yəni seed/JWT/CORS dev tənzimləmələrini toxunmadan) production-da
// da açıla bilər - məs. Render-də Swagger__Enabled=true environment variable-ı ilə.
var swaggerEnabled = app.Configuration.GetValue<bool?>("Swagger:Enabled") ?? app.Environment.IsDevelopment();
if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "MIMEDU.AZ API v1");
        options.DocumentTitle = "MIMEDU.AZ API";
    });
}

app.UseHttpsRedirection();

// Yüklənmiş resurs faylları /uploads/... altında statik olaraq verilir.
app.UseStaticFiles();

app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Migration hər mühitdə tətbiq olunur ki, production-da yeni migration üçün əl ilə
// müdaxilə lazım olmasın. Seed data isə yalnız Development-da - demo istifadəçi/
// resurslar production bazasına düşməsin.
await ApplyMigrationsAsync(app);

if (app.Environment.IsDevelopment())
{
    await SeedDevelopmentDataAsync(app);
}

app.Run();

static async Task ApplyMigrationsAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        // Bazaya çıxış yoxdursa tətbiq yenə də qalxsın - Swagger və konfiqurasiya yoxlanıla bilsin.
        logger.LogError(ex, "Migration mərhələsi uğursuz oldu. Verilənlər bazası əlçatandırmı?");
    }
}

static async Task SeedDevelopmentDataAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
        await seeder.SeedAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Seed mərhələsi uğursuz oldu.");
    }
}

/// <summary>Integration testlərdən WebApplicationFactory ilə istifadə üçün.</summary>
public partial class Program;
