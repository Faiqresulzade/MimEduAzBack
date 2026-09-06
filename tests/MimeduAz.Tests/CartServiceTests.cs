using FluentAssertions;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Services;
using MimeduAz.Contracts.Carts;
using MimeduAz.Domain.Entities;
using MimeduAz.Domain.Enums;
using MimeduAz.Infrastructure.Persistence;
using MimeduAz.Tests.TestSupport;

namespace MimeduAz.Tests;

public sealed class CartServiceTests
{
    private static readonly Guid BuyerId = Guid.NewGuid();
    private static readonly Guid AuthorId = Guid.NewGuid();

    [Fact]
    public async Task Adding_a_paid_resource_snapshots_its_current_price()
    {
        await using var db = TestHarness.CreateDb();
        var resource = await SeedPaidResourceAsync(db, price: 8.50m);
        var sut = CreateSut(db);

        var cart = await sut.AddItemAsync(
            new AddCartItemRequest(CatalogItemType.Resource, resource.Id), CancellationToken.None);

        cart.Items.Should().ContainSingle();
        cart.Items.Single().Price.Should().Be(8.50m);
        cart.Total.Should().Be(8.50m);

        // Qiymət sonradan dəyişsə də səbətdəki snapshot qalır.
        resource.Price = 20m;
        await db.SaveChangesAsync();

        var reloaded = await sut.GetAsync(CancellationToken.None);
        reloaded.Items.Single().Price.Should().Be(8.50m);
    }

    [Fact]
    public async Task Free_resources_cannot_be_added_to_the_cart()
    {
        await using var db = TestHarness.CreateDb();
        var resource = await SeedPaidResourceAsync(db, price: 0m, isPaid: false);

        var act = () => CreateSut(db).AddItemAsync(
            new AddCartItemRequest(CatalogItemType.Resource, resource.Id), CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*Pulsuz resursu*");
    }

    [Fact]
    public async Task Unapproved_resources_cannot_be_added_to_the_cart()
    {
        await using var db = TestHarness.CreateDb();
        var resource = await SeedPaidResourceAsync(db, price: 5m, status: ResourceStatus.Pending);

        var act = () => CreateSut(db).AddItemAsync(
            new AddCartItemRequest(CatalogItemType.Resource, resource.Id), CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*təsdiqlənməyib*");
    }

    [Fact]
    public async Task Authors_cannot_buy_their_own_resource()
    {
        await using var db = TestHarness.CreateDb();
        var resource = await SeedPaidResourceAsync(db, price: 5m);

        var authorCart = new CartService(db, new FakeCurrentUserService(AuthorId));

        var act = () => authorCart.AddItemAsync(
            new AddCartItemRequest(CatalogItemType.Resource, resource.Id), CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*Öz resursunuzu*");
    }

    [Fact]
    public async Task The_same_item_cannot_be_added_twice()
    {
        await using var db = TestHarness.CreateDb();
        var resource = await SeedPaidResourceAsync(db, price: 5m);
        var sut = CreateSut(db);

        await sut.AddItemAsync(new AddCartItemRequest(CatalogItemType.Resource, resource.Id), CancellationToken.None);

        var act = () => sut.AddItemAsync(
            new AddCartItemRequest(CatalogItemType.Resource, resource.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task A_full_live_training_cannot_be_added()
    {
        await using var db = TestHarness.CreateDb();
        await SeedPaidResourceAsync(db, price: 5m);

        var training = new Training
        {
            Name = "Canlı emalatxana",
            Format = TrainingFormat.Live,
            Description = "Yerlər məhduddur",
            Price = 120m,
            DurationHours = 8,
            MetaLabel = "2 gün · 8 saat",
            SeatLimit = 1
        };
        db.Trainings.Add(training);
        db.Enrollments.Add(new Enrollment { UserId = AuthorId, TrainingId = training.Id });
        await db.SaveChangesAsync();

        var act = () => CreateSut(db).AddItemAsync(
            new AddCartItemRequest(CatalogItemType.Training, training.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*boş yer qalmayıb*");
    }

    [Fact]
    public async Task Removing_an_item_updates_the_total()
    {
        await using var db = TestHarness.CreateDb();
        var resource = await SeedPaidResourceAsync(db, price: 8.50m);
        var sut = CreateSut(db);

        var cart = await sut.AddItemAsync(
            new AddCartItemRequest(CatalogItemType.Resource, resource.Id), CancellationToken.None);

        var afterRemoval = await sut.RemoveItemAsync(cart.Items.Single().Id, CancellationToken.None);

        afterRemoval.Items.Should().BeEmpty();
        afterRemoval.Total.Should().Be(0m);
    }

    private static CartService CreateSut(ApplicationDbContext db) =>
        new(db, new FakeCurrentUserService(BuyerId));

    private static async Task<Resource> SeedPaidResourceAsync(
        ApplicationDbContext db,
        decimal price,
        bool isPaid = true,
        ResourceStatus status = ResourceStatus.Approved)
    {
        if (!db.Users.Any())
        {
            db.Users.AddRange(
                new ApplicationUser { Id = BuyerId, FullName = "Alıcı", Email = "b@mimedu.az" },
                new ApplicationUser { Id = AuthorId, FullName = "Müəllif", Email = "a@mimedu.az" });
        }

        var resource = new Resource
        {
            Name = "Mexanika test bankı",
            Subject = "Fizika",
            Grade = 9,
            Type = ResourceType.Test,
            AuthorId = AuthorId,
            IsPaid = isPaid,
            Price = price,
            Status = status
        };

        db.Resources.Add(resource);
        await db.SaveChangesAsync();
        return resource;
    }
}
