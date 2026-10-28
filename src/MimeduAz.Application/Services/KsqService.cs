using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Mappings;
using MimeduAz.Application.Common.Options;
using MimeduAz.Application.Common.Utilities;
using MimeduAz.Contracts.Ksq;
using MimeduAz.Domain.Entities;

namespace MimeduAz.Application.Services;

public sealed class KsqService : IKsqService
{
    private static readonly string[] AllowedImageTypes = { "image/jpeg", "image/png", "image/webp" };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IKsqCardReader _reader;
    private readonly IKsqScanRateLimiter _rateLimiter;
    private readonly KsqOptions _options;

    public KsqService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IKsqCardReader reader,
        IKsqScanRateLimiter rateLimiter,
        IOptions<KsqOptions> options)
    {
        _db = db;
        _currentUser = currentUser;
        _reader = reader;
        _rateLimiter = rateLimiter;
        _options = options.Value;
    }

    // ------------------------------------------------------------ Testlər (açar)

    public async Task<IReadOnlyList<KsqTestDto>> GetTestsAsync(CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var tests = await _db.KsqTests
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);

        return tests.Select(t => t.ToDto()).ToList();
    }

    public async Task<KsqTestDto> CreateTestAsync(SaveKsqTestRequest request, CancellationToken ct)
    {
        var test = new KsqTest { UserId = _currentUser.RequireUserId() };
        Apply(test, request);

        _db.KsqTests.Add(test);
        await _db.SaveChangesAsync(ct);

        return test.ToDto();
    }

    public async Task<KsqTestDto> UpdateTestAsync(Guid id, SaveKsqTestRequest request, CancellationToken ct)
    {
        var test = await FindOwnTestAsync(id, ct);
        Apply(test, request);
        test.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return test.ToDto();
    }

    public async Task DeleteTestAsync(Guid id, CancellationToken ct)
    {
        var test = await FindOwnTestAsync(id, ct);

        // InMemory provayderi DB səviyyəsində cascade etmir - nəticələri açıq silirik.
        var results = await _db.KsqResults.Where(r => r.TestId == id).ToListAsync(ct);
        _db.KsqResults.RemoveRange(results);
        _db.KsqTests.Remove(test);

        await _db.SaveChangesAsync(ct);
    }

    private static void Apply(KsqTest test, SaveKsqTestRequest request)
    {
        var closed = request.ClosedPoints.Count;

        test.Name = request.Name.Trim();
        test.ClosedPoints = request.ClosedPoints.ToList();
        test.OpenPoints = request.OpenPoints.ToList();
        test.KeyA = KsqScoring.Normalize(request.KeyA.Select(KsqScoring.NormalizeLetter), closed, null);
        test.KeyB = KsqScoring.Normalize(request.KeyB.Select(KsqScoring.NormalizeLetter), closed, null);
    }

    private async Task<KsqTest> FindOwnTestAsync(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        // Başqasının testi "tapılmadı" kimi qaytarılır - mövcudluğu da gizlədilir.
        return await _db.KsqTests.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct)
               ?? throw NotFoundException.For("Test", id);
    }

    // ------------------------------------------------------------ Nəticələr

    public async Task<KsqResultListDto> GetResultsAsync(Guid? testId, string? sinif, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var scope = _db.KsqResults.AsNoTracking().Where(r => r.UserId == userId);
        if (testId is not null)
        {
            scope = scope.Where(r => r.TestId == testId);
        }

        // Sinif seçimi testə görə daralır, amma seçilmiş sinifdən asılı olmur.
        var classes = await scope
            .Where(r => r.Sinif != string.Empty)
            .Select(r => r.Sinif)
            .Distinct()
            .ToListAsync(ct);

        var items = scope;
        if (!string.IsNullOrWhiteSpace(sinif))
        {
            var value = sinif.Trim();
            items = items.Where(r => r.Sinif == value);
        }

        var results = await items.OrderByDescending(r => r.CreatedAt).ToListAsync(ct);

        return new KsqResultListDto(
            results.Select(r => r.ToDto()).ToList(),
            classes.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList());
    }

    public async Task<KsqResultDto> SaveResultAsync(SaveKsqResultRequest request, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();
        var test = await FindOwnTestAsync(request.TestId, ct);

        var result = Build(userId, test, request);
        _db.KsqResults.Add(result);
        await _db.SaveChangesAsync(ct);

        return result.ToDto();
    }

    public async Task<IReadOnlyList<KsqResultDto>> SaveResultsAsync(
        SaveKsqResultsBulkRequest request, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var testIds = request.Items.Select(i => i.TestId).Distinct().ToList();
        var tests = await _db.KsqTests
            .Where(t => t.UserId == userId && testIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, ct);

        var missing = testIds.FirstOrDefault(id => !tests.ContainsKey(id));
        if (missing != Guid.Empty)
        {
            throw NotFoundException.For("Test", missing);
        }

        // Əvvəl hamısı yoxlanılır - biri səhvdirsə heç biri saxlanmır.
        var results = request.Items.Select(item => Build(userId, tests[item.TestId], item)).ToList();

        _db.KsqResults.AddRange(results);
        await _db.SaveChangesAsync(ct);

        return results.Select(r => r.ToDto()).ToList();
    }

    public async Task DeleteResultAsync(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var result = await _db.KsqResults.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct)
                     ?? throw NotFoundException.For("Nəticə", id);

        _db.KsqResults.Remove(result);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Balı client-dən götürmür - açara görə yenidən hesablayır.</summary>
    private static KsqResult Build(Guid userId, KsqTest test, SaveKsqResultRequest request)
    {
        var variant = request.Variant.Trim().ToUpperInvariant();
        var key = variant == "B" ? test.KeyB : test.KeyA;

        if (!KsqScoring.KeyReady(key, test.ClosedPoints.Count))
        {
            throw new BadRequestException($"\"{test.Name}\" testinin {variant} variantının açarı hazır deyil.");
        }

        var answers = KsqScoring.Normalize(
            request.Cavablar.Select(KsqScoring.NormalizeLetter), test.ClosedPoints.Count, null);

        var openScores = KsqScoring.Normalize<decimal?>(request.Aciq, test.OpenPoints.Count, null)
            .Select((value, j) => Math.Clamp(value ?? 0, 0, test.OpenPoints[j]))
            .ToList();

        var score = KsqScoring.Compute(
            test.ClosedPoints, test.OpenPoints, key, answers, openScores.Select(v => (decimal?)v).ToList());

        return new KsqResult
        {
            UserId = userId,
            TestId = test.Id,
            TestName = test.Name,
            Sinif = request.Sinif?.Trim() ?? string.Empty,
            Ad = request.Ad?.Trim() ?? string.Empty,
            Variant = variant,
            Kod = request.Kod?.Trim() ?? string.Empty,
            Answers = answers,
            OpenScores = openScores,
            Score = score.Bal,
            MaxScore = score.Max,
            WrongQuestions = score.Sehv.ToList()
        };
    }

    // ------------------------------------------------------------ Şəkil oxuma

    public async Task<KsqScanResultDto> ScanAsync(
        Stream image, string contentType, long length, int closedCount, int openCount, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        if (closedCount is < 1 or > KsqScoring.MaxClosed)
        {
            throw new ValidationFailedException("closedCount", $"Qapalı sual sayı 1-{KsqScoring.MaxClosed} aralığında olmalıdır.");
        }

        if (openCount is < 0 or > KsqScoring.MaxOpen)
        {
            throw new ValidationFailedException("openCount", $"Açıq sual sayı 0-{KsqScoring.MaxOpen} aralığında olmalıdır.");
        }

        var type = contentType.Split(';')[0].Trim().ToLowerInvariant();
        if (!AllowedImageTypes.Contains(type))
        {
            throw new ValidationFailedException("image", "Yalnız JPEG, PNG və ya WEBP şəkil qəbul olunur.");
        }

        if (length <= 0 || length > _options.MaxImageBytes)
        {
            throw new ValidationFailedException(
                "image", $"Şəklin ölçüsü {_options.MaxImageBytes / (1024 * 1024)} MB-dan çox ola bilməz.");
        }

        _rateLimiter.Acquire(userId);

        // Şəkil yalnız bu metodun ömrü boyu yaddaşda qalır.
        using var buffer = new MemoryStream((int)length);
        await image.CopyToAsync(buffer, ct);

        return await _reader.ReadAsync(buffer.ToArray(), type, closedCount, openCount, ct);
    }
}
