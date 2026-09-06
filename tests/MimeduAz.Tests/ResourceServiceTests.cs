using System.Text;
using FluentAssertions;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Services;
using MimeduAz.Contracts.Admin;
using MimeduAz.Contracts.Resources;
using MimeduAz.Domain.Entities;
using MimeduAz.Domain.Enums;
using MimeduAz.Infrastructure.Persistence;
using MimeduAz.Tests.TestSupport;

namespace MimeduAz.Tests;

public sealed class ResourceServiceTests
{
    private static readonly Guid AuthorId = Guid.NewGuid();

    [Fact]
    public async Task Uploaded_resource_starts_in_pending_status()
    {
        await using var db = TestHarness.CreateDb();
        await SeedAuthorAsync(db);

        var created = await CreateSut(db, AuthorId)
            .CreateAsync(Request(), Upload("plan.pdf"), CancellationToken.None);

        created.Status.Should().Be(ResourceStatus.Pending);
        created.ApprovedAt.Should().BeNull();
        db.Resources.Single().FilePath.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Free_resource_price_is_forced_to_zero()
    {
        await using var db = TestHarness.CreateDb();
        await SeedAuthorAsync(db);

        var request = Request();
        request.IsPaid = false;
        request.Price = 25m;

        var created = await CreateSut(db, AuthorId)
            .CreateAsync(request, Upload("plan.pdf"), CancellationToken.None);

        created.Price.Should().Be(0m);
    }

    [Theory]
    [InlineData("virus.exe")]
    [InlineData("qeyd.txt")]
    [InlineData("arxiv.zip")]
    public async Task Disallowed_file_extensions_are_rejected(string fileName)
    {
        await using var db = TestHarness.CreateDb();
        await SeedAuthorAsync(db);

        var act = () => CreateSut(db, AuthorId)
            .CreateAsync(Request(), Upload(fileName), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationFailedException>();
    }

    [Fact]
    public async Task Files_above_the_size_limit_are_rejected()
    {
        await using var db = TestHarness.CreateDb();
        await SeedAuthorAsync(db);

        var oversized = new ResourceFileUpload(
            new MemoryStream(new byte[10]), "boyuk.pdf", 26 * 1024 * 1024, "application/pdf");

        var act = () => CreateSut(db, AuthorId).CreateAsync(Request(), oversized, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationFailedException>()
            .WithMessage("*düzgün deyil*");
    }

    [Fact]
    public async Task Public_listing_returns_only_approved_resources()
    {
        await using var db = TestHarness.CreateDb();
        await SeedAuthorAsync(db);
        db.Resources.AddRange(
            Resource("Təsdiqlənmiş", ResourceStatus.Approved),
            Resource("Gözləyən", ResourceStatus.Pending),
            Resource("Rədd edilmiş", ResourceStatus.Rejected));
        await db.SaveChangesAsync();

        var result = await CreateSut(db, userId: null)
            .GetAsync(new ResourceQuery(), CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items.Single().Name.Should().Be("Təsdiqlənmiş");
    }

    [Fact]
    public async Task Downloading_a_free_resource_increments_the_counter()
    {
        await using var db = TestHarness.CreateDb();
        await SeedAuthorAsync(db);
        var resource = Resource("Pulsuz vərəq", ResourceStatus.Approved);
        resource.FilePath = "uploads/resources/x.pdf";
        db.Resources.Add(resource);
        await db.SaveChangesAsync();

        var sut = CreateSut(db, userId: null);
        await sut.DownloadAsync(resource.Id, CancellationToken.None);
        var second = await sut.DownloadAsync(resource.Id, CancellationToken.None);

        second.Downloads.Should().Be(2);
        db.Resources.Single().Downloads.Should().Be(2);
    }

    [Fact]
    public async Task Paid_resource_cannot_be_downloaded_without_a_purchase()
    {
        await using var db = TestHarness.CreateDb();
        await SeedAuthorAsync(db);
        var resource = Resource("Ödənişli vərəq", ResourceStatus.Approved);
        resource.IsPaid = true;
        resource.Price = 8m;
        resource.FilePath = "uploads/resources/x.pdf";
        db.Resources.Add(resource);
        await db.SaveChangesAsync();

        var buyerId = Guid.NewGuid();
        var act = () => CreateSut(db, buyerId).DownloadAsync(resource.Id, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Approving_a_resource_makes_it_public_and_sets_approved_at()
    {
        await using var db = TestHarness.CreateDb();
        await SeedAuthorAsync(db);
        var resource = Resource("Gözləyən", ResourceStatus.Pending);
        db.Resources.Add(resource);
        await db.SaveChangesAsync();

        var admin = new AdminService(db, TestHarness.Commission(), TestHarness.Logger<AdminService>());

        var approved = await admin.ApproveResourceAsync(resource.Id, CancellationToken.None);

        approved.Status.Should().Be(ResourceStatus.Approved);
        approved.ApprovedAt.Should().NotBeNull();

        var listing = await CreateSut(db, userId: null).GetAsync(new ResourceQuery(), CancellationToken.None);
        listing.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Rejecting_a_resource_stores_the_reason_and_keeps_it_hidden()
    {
        await using var db = TestHarness.CreateDb();
        await SeedAuthorAsync(db);
        var resource = Resource("Gözləyən", ResourceStatus.Pending);
        db.Resources.Add(resource);
        await db.SaveChangesAsync();

        var admin = new AdminService(db, TestHarness.Commission(), TestHarness.Logger<AdminService>());

        var rejected = await admin.RejectResourceAsync(
            resource.Id, new RejectResourceRequest { Reason = "Orfoqrafik səhvlər" }, CancellationToken.None);

        rejected.Status.Should().Be(ResourceStatus.Rejected);
        rejected.RejectionReason.Should().Be("Orfoqrafik səhvlər");

        var listing = await CreateSut(db, userId: null).GetAsync(new ResourceQuery(), CancellationToken.None);
        listing.TotalCount.Should().Be(0);
    }

    private static ResourceService CreateSut(ApplicationDbContext db, Guid? userId, bool isAdmin = false) => new(
        db,
        new FakeFileStorageService(),
        new FakeCurrentUserService(userId, isAdmin),
        TestHarness.FileStorage(),
        TestHarness.Logger<ResourceService>());

    private static CreateResourceRequest Request() => new()
    {
        Name = "Onluq kəsrlər iş vərəqi",
        Subject = "Riyaziyyat",
        Grade = 5,
        Type = ResourceType.WorkSheet,
        IsPaid = false,
        Price = 0m
    };

    private static ResourceFileUpload Upload(string fileName)
    {
        var bytes = Encoding.UTF8.GetBytes("nümunə fayl məzmunu");
        return new ResourceFileUpload(new MemoryStream(bytes), fileName, bytes.Length, "application/pdf");
    }

    private static Resource Resource(string name, ResourceStatus status) => new()
    {
        Name = name,
        Subject = "Riyaziyyat",
        Grade = 5,
        Type = ResourceType.WorkSheet,
        AuthorId = AuthorId,
        Status = status,
        ApprovedAt = status == ResourceStatus.Approved ? DateTime.UtcNow : null
    };

    private static async Task SeedAuthorAsync(ApplicationDbContext db)
    {
        db.Users.Add(new ApplicationUser { Id = AuthorId, FullName = "Müəllif", Email = "a@mimedu.az" });
        await db.SaveChangesAsync();
    }
}
