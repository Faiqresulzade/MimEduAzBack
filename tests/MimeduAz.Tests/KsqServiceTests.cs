using FluentAssertions;
using Microsoft.Extensions.Options;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Options;
using MimeduAz.Application.Common.Utilities;
using MimeduAz.Application.Services;
using MimeduAz.Contracts.Ksq;
using MimeduAz.Infrastructure.Ai;
using MimeduAz.Infrastructure.Persistence;
using MimeduAz.Tests.TestSupport;

namespace MimeduAz.Tests;

public sealed class KsqServiceTests
{
    private static readonly Guid TeacherId = Guid.NewGuid();
    private static readonly Guid OtherTeacherId = Guid.NewGuid();

    // "15 sual (14+1)" preseti
    private static readonly int[] Closed15 = { 4, 4, 4, 4, 4, 6, 6, 6, 6, 6, 7, 7, 8, 8 };
    private static readonly int[] Open15 = { 20 };

    // ------------------------------------------------------------ Bal hesablanması

    [Fact]
    public void Compute_adds_points_of_correct_answers_and_lists_wrong_and_blank_questions()
    {
        var key = new string?[] { "A", "B", "C", "D" };
        var answers = new string?[] { "A", "C", null, "D" };

        var score = KsqScoring.Compute(new[] { 2, 3, 5, 7 }, new[] { 10 }, key, answers, new decimal?[] { 4 });

        score.Bal.Should().Be(2 + 7 + 4);
        score.Max.Should().Be(27);
        score.Sehv.Should().Equal(2, 3);
    }

    [Fact]
    public void Compute_clamps_open_scores_between_zero_and_question_max()
    {
        var score = KsqScoring.Compute(
            new[] { 1 }, new[] { 10, 10 }, new string?[] { "A" }, new string?[] { "B" }, new decimal?[] { 15, -3 });

        score.Bal.Should().Be(10);
    }

    [Theory]
    [InlineData("a", "A")]
    [InlineData(" E ", "E")]
    [InlineData("C)", "C")]
    [InlineData("F", null)]
    [InlineData("AB", null)]
    [InlineData("", null)]
    public void NormalizeLetter_accepts_only_single_A_to_E(string input, string? expected) =>
        KsqScoring.NormalizeLetter(input).Should().Be(expected);

    // ------------------------------------------------------------ Testlər

    [Fact]
    public async Task Tests_are_visible_only_to_their_owner()
    {
        await using var db = TestHarness.CreateDb();
        await CreateSut(db, TeacherId).CreateTestAsync(Request("Riyaziyyat KSQ-1"), CancellationToken.None);

        var mine = await CreateSut(db, TeacherId).GetTestsAsync(CancellationToken.None);
        var theirs = await CreateSut(db, OtherTeacherId).GetTestsAsync(CancellationToken.None);

        mine.Should().ContainSingle(t => t.Name == "Riyaziyyat KSQ-1");
        theirs.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_normalizes_keys_to_closed_question_count()
    {
        await using var db = TestHarness.CreateDb();
        var request = Request("Test");
        request.KeyA = new List<string?> { "a", "b" };
        request.KeyB = new List<string?>();

        var test = await CreateSut(db, TeacherId).CreateTestAsync(request, CancellationToken.None);

        test.KeyA.Should().HaveCount(Closed15.Length);
        test.KeyA.Take(2).Should().Equal("A", "B");
        test.KeyA.Skip(2).Should().OnlyContain(k => k == null);
        test.KeyB.Should().HaveCount(Closed15.Length);
    }

    [Fact]
    public async Task Other_teacher_cannot_update_or_delete_test()
    {
        await using var db = TestHarness.CreateDb();
        var test = await CreateSut(db, TeacherId).CreateTestAsync(Request("Test"), CancellationToken.None);
        var other = CreateSut(db, OtherTeacherId);

        var update = () => other.UpdateTestAsync(test.Id, Request("Oğurlandı"), CancellationToken.None);
        var delete = () => other.DeleteTestAsync(test.Id, CancellationToken.None);

        await update.Should().ThrowAsync<NotFoundException>();
        await delete.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Deleting_test_deletes_its_results()
    {
        await using var db = TestHarness.CreateDb();
        var sut = CreateSut(db, TeacherId);
        var test = await sut.CreateTestAsync(ReadyRequest("Test"), CancellationToken.None);
        await sut.SaveResultAsync(Result(test.Id, "9A"), CancellationToken.None);

        await sut.DeleteTestAsync(test.Id, CancellationToken.None);

        db.KsqTests.Should().BeEmpty();
        db.KsqResults.Should().BeEmpty();
    }

    // ------------------------------------------------------------ Nəticələr

    [Fact]
    public async Task SaveResult_recomputes_score_on_server()
    {
        await using var db = TestHarness.CreateDb();
        var sut = CreateSut(db, TeacherId);
        var test = await sut.CreateTestAsync(ReadyRequest("Test"), CancellationToken.None);

        // Bütün cavablar "A" (açar), 1-ci və 14-cü sual səhv, açıq sualda 25 (max 20-yə kəsilir).
        var request = Result(test.Id, "9A");
        request.Cavablar[0] = "B";
        request.Cavablar[13] = null;
        request.Aciq = new List<decimal?> { 25 };

        var result = await sut.SaveResultAsync(request, CancellationToken.None);

        result.Max.Should().Be(Closed15.Sum() + 20);
        result.Bal.Should().Be(Closed15.Sum() - 4 - 8 + 20);
        result.Sehv.Should().Equal(1, 14);
        result.Aciq.Should().Equal(20m);
        result.Test.Should().Be("Test");
    }

    [Fact]
    public async Task SaveResult_rejects_variant_whose_key_is_not_ready()
    {
        await using var db = TestHarness.CreateDb();
        var sut = CreateSut(db, TeacherId);
        var request = ReadyRequest("Test");
        request.KeyB = new List<string?> { "A" };
        var test = await sut.CreateTestAsync(request, CancellationToken.None);

        var result = Result(test.Id, "9A");
        result.Variant = "B";

        var act = () => sut.SaveResultAsync(result, CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*B variantının açarı hazır deyil*");
    }

    [Fact]
    public async Task Bulk_save_is_all_or_nothing()
    {
        await using var db = TestHarness.CreateDb();
        var sut = CreateSut(db, TeacherId);
        var test = await sut.CreateTestAsync(ReadyRequest("Test"), CancellationToken.None);

        var bad = Result(test.Id, "9A");
        bad.Variant = "B"; // B açarı boşdur

        var act = () => sut.SaveResultsAsync(
            new SaveKsqResultsBulkRequest { Items = { Result(test.Id, "9A"), bad } }, CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>();
        db.KsqResults.Should().BeEmpty();
    }

    [Fact]
    public async Task GetResults_filters_by_test_and_class_and_returns_classes_of_test()
    {
        await using var db = TestHarness.CreateDb();
        var sut = CreateSut(db, TeacherId);
        var first = await sut.CreateTestAsync(ReadyRequest("Birinci"), CancellationToken.None);
        var second = await sut.CreateTestAsync(ReadyRequest("İkinci"), CancellationToken.None);

        await sut.SaveResultsAsync(new SaveKsqResultsBulkRequest
        {
            Items = { Result(first.Id, "9A"), Result(first.Id, "9B"), Result(second.Id, "10A") }
        }, CancellationToken.None);

        var filtered = await sut.GetResultsAsync(first.Id, "9B", CancellationToken.None);
        var otherTeacher = await CreateSut(db, OtherTeacherId).GetResultsAsync(null, null, CancellationToken.None);

        filtered.Items.Should().ContainSingle(r => r.Sinif == "9B");
        filtered.Classes.Should().Equal("9A", "9B");
        otherTeacher.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Other_teacher_cannot_delete_result()
    {
        await using var db = TestHarness.CreateDb();
        var sut = CreateSut(db, TeacherId);
        var test = await sut.CreateTestAsync(ReadyRequest("Test"), CancellationToken.None);
        var saved = await sut.SaveResultAsync(Result(test.Id, "9A"), CancellationToken.None);

        var act = () => CreateSut(db, OtherTeacherId).DeleteResultAsync(saved.Id, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        db.KsqResults.Should().ContainSingle();
    }

    // ------------------------------------------------------------ Şəkil oxuma

    [Fact]
    public async Task Scan_rejects_unsupported_image_type_before_calling_ai()
    {
        await using var db = TestHarness.CreateDb();
        var reader = new FakeCardReader();
        var sut = CreateSut(db, TeacherId, reader);

        var act = () => sut.ScanAsync(new MemoryStream(new byte[10]), "application/pdf", 10, 14, 1, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationFailedException>();
        reader.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Scan_stops_after_per_minute_limit()
    {
        await using var db = TestHarness.CreateDb();
        var reader = new FakeCardReader();
        var limiter = new KsqScanRateLimiter(
            Options.Create(new KsqOptions { ScansPerMinute = 2, ScansPerDay = 100 }), TimeProvider.System);
        var sut = CreateSut(db, TeacherId, reader, limiter);

        Task<KsqScanResultDto> Scan() =>
            sut.ScanAsync(new MemoryStream(new byte[10]), "image/jpeg", 10, 14, 1, CancellationToken.None);

        await Scan();
        await Scan();
        var third = () => Scan();

        await third.Should().ThrowAsync<TooManyRequestsException>();
        reader.Calls.Should().Be(2);
    }

    [Fact]
    public void RateLimiter_daily_limit_resets_after_a_day()
    {
        var clock = new ManualClock(DateTimeOffset.Parse("2026-10-28T08:00:00Z"));
        var limiter = new KsqScanRateLimiter(
            Options.Create(new KsqOptions { ScansPerMinute = 100, ScansPerDay = 2 }), clock);

        limiter.Acquire(TeacherId);
        limiter.Acquire(TeacherId);
        var blocked = () => limiter.Acquire(TeacherId);
        blocked.Should().Throw<TooManyRequestsException>().WithMessage("*Sutkalıq*");

        clock.Now = clock.Now.AddDays(1).AddSeconds(1);
        limiter.Acquire(TeacherId);
    }

    // ------------------------------------------------------------ AI cavabının parse-ı

    [Fact]
    public void Parser_reads_fenced_json_and_normalizes_arrays()
    {
        const string content = """
            Budur:
            ```json
            {"ad":" Əli Məmmədov ","variant":"b","kod":"1234","cavablar":["a","B","X",null],"aciq":["12"],"unsure":[3, "4", 99],"qeyd":null}
            ```
            """;

        var result = KsqCardResponseParser.TryParse(content, closedCount: 5, openCount: 2);

        result.Should().NotBeNull();
        result!.Ad.Should().Be("Əli Məmmədov");
        result.Variant.Should().Be("B");
        result.Kod.Should().Be("1234");
        result.Cavablar.Should().Equal("A", "B", null, null, null);
        result.AciqCavablar.Should().Equal("12", null);
        result.Unsure.Should().Equal(3, 4);
        result.Qeyd.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bağışlayın, oxuya bilmədim.")]
    [InlineData("{ bu json deyil }")]
    public void Parser_returns_null_for_unreadable_content(string? content) =>
        KsqCardResponseParser.TryParse(content, 14, 1).Should().BeNull();

    // ------------------------------------------------------------ Köməkçilər

    private static KsqService CreateSut(
        ApplicationDbContext db,
        Guid userId,
        IKsqCardReader? reader = null,
        IKsqScanRateLimiter? limiter = null) =>
        new(
            db,
            new FakeCurrentUserService(userId),
            reader ?? new FakeCardReader(),
            limiter ?? new KsqScanRateLimiter(Options.Create(new KsqOptions()), TimeProvider.System),
            Options.Create(new KsqOptions()));

    private static SaveKsqTestRequest Request(string name) => new()
    {
        Name = name,
        ClosedPoints = Closed15.ToList(),
        OpenPoints = Open15.ToList()
    };

    /// <summary>A variantının bütün cavabları "A"; B boşdur.</summary>
    private static SaveKsqTestRequest ReadyRequest(string name)
    {
        var request = Request(name);
        request.KeyA = Enumerable.Repeat<string?>("A", Closed15.Length).ToList();
        return request;
    }

    private static SaveKsqResultRequest Result(Guid testId, string sinif) => new()
    {
        TestId = testId,
        Sinif = sinif,
        Ad = "Şagird",
        Variant = "A",
        Cavablar = Enumerable.Repeat<string?>("A", Closed15.Length).ToList(),
        Aciq = new List<decimal?> { 0 }
    };

    private sealed class FakeCardReader : IKsqCardReader
    {
        public int Calls { get; private set; }

        public Task<KsqScanResultDto> ReadAsync(
            byte[] image, string contentType, int closedCount, int openCount, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new KsqScanResultDto(
                "Şagird", "A", null,
                Enumerable.Repeat<string?>("A", closedCount).ToList(),
                Enumerable.Repeat<string?>(null, openCount).ToList(),
                Array.Empty<int>(), null));
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        public ManualClock(DateTimeOffset now) => Now = now;

        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
