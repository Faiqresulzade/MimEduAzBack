using FluentAssertions;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Services;
using MimeduAz.Contracts.Orders;
using MimeduAz.Domain.Entities;
using MimeduAz.Domain.Enums;
using MimeduAz.Infrastructure.Persistence;
using MimeduAz.Tests.TestSupport;

namespace MimeduAz.Tests;

public sealed class OrderServiceTests
{
    private static readonly Guid BuyerId = Guid.NewGuid();
    private static readonly Guid AuthorId = Guid.NewGuid();

    [Fact]
    public async Task Checkout_paid_resource_splits_20_percent_commission_and_80_percent_to_author()
    {
        await using var db = TestHarness.CreateDb();
        var resource = await SeedAsync(db, resourcePrice: 10.00m);

        var sut = CreateSut(db);

        var order = await sut.CheckoutAsync(new CheckoutRequest(), CancellationToken.None);

        order.TotalAmount.Should().Be(10.00m);

        var line = db.OrderItems.Single(i => i.ItemId == resource.Id);
        line.CommissionAmount.Should().Be(2.00m);
        line.AuthorPayoutAmount.Should().Be(8.00m);
        db.Orders.Single().CommissionAmount.Should().Be(2.00m);
    }

    [Fact]
    public async Task Checkout_rounds_commission_to_two_decimals()
    {
        await using var db = TestHarness.CreateDb();
        await SeedAsync(db, resourcePrice: 8.55m);

        var order = await CreateSut(db).CheckoutAsync(new CheckoutRequest(), CancellationToken.None);

        var line = db.OrderItems.Single();
        // 8.55 * 0.20 = 1.71
        line.CommissionAmount.Should().Be(1.71m);
        line.AuthorPayoutAmount.Should().Be(6.84m);
        (line.CommissionAmount + line.AuthorPayoutAmount).Should().Be(order.TotalAmount);
    }

    [Fact]
    public async Task Checkout_creates_enrollment_for_training_items_and_takes_no_commission()
    {
        await using var db = TestHarness.CreateDb();
        var (_, training) = await SeedWithTrainingAsync(db);

        await CreateSut(db).CheckoutAsync(new CheckoutRequest(), CancellationToken.None);

        var enrollment = db.Enrollments.Single(e => e.UserId == BuyerId && e.TrainingId == training.Id);
        enrollment.Status.Should().Be(EnrollmentStatus.InProgress);
        enrollment.ProgressPercent.Should().Be(0);

        var line = db.OrderItems.Single(i => i.ItemType == CatalogItemType.Training);
        line.CommissionAmount.Should().Be(0m);
        line.AuthorPayoutAmount.Should().Be(0m);
    }

    [Fact]
    public async Task Checkout_empties_the_cart_and_marks_order_paid()
    {
        await using var db = TestHarness.CreateDb();
        await SeedAsync(db, resourcePrice: 5.00m);

        var order = await CreateSut(db).CheckoutAsync(new CheckoutRequest(), CancellationToken.None);

        order.Status.Should().Be(OrderStatus.Paid);
        order.Code.Should().MatchRegex(@"^MIM-\d{4,6}$");
        db.CartItems.Should().BeEmpty();
    }

    [Fact]
    public async Task Checkout_with_empty_cart_throws()
    {
        await using var db = TestHarness.CreateDb();
        db.Users.Add(new ApplicationUser { Id = BuyerId, FullName = "Alıcı Müəllim", Email = "b@mimedu.az" });
        await db.SaveChangesAsync();

        var act = () => CreateSut(db).CheckoutAsync(new CheckoutRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*Səbət boşdur*");
    }

    [Fact]
    public async Task Checkout_uses_customer_name_from_request_when_provided()
    {
        await using var db = TestHarness.CreateDb();
        await SeedAsync(db, resourcePrice: 5.00m);

        var order = await CreateSut(db)
            .CheckoutAsync(new CheckoutRequest { CustomerName = "Xüsusi Ad" }, CancellationToken.None);

        order.CustomerName.Should().Be("Xüsusi Ad");
    }

    private static OrderService CreateSut(ApplicationDbContext db) => new(
        db,
        new FakeCurrentUserService(BuyerId),
        TestHarness.Commission(),
        TestHarness.Logger<OrderService>());

    private static async Task<Resource> SeedAsync(ApplicationDbContext db, decimal resourcePrice)
    {
        db.Users.AddRange(
            new ApplicationUser { Id = BuyerId, FullName = "Alıcı Müəllim", Email = "b@mimedu.az" },
            new ApplicationUser { Id = AuthorId, FullName = "Müəllif Müəllim", Email = "a@mimedu.az" });

        var resource = new Resource
        {
            Name = "Ödənişli iş vərəqi",
            Subject = "Riyaziyyat",
            Grade = 5,
            Type = ResourceType.WorkSheet,
            AuthorId = AuthorId,
            IsPaid = true,
            Price = resourcePrice,
            Status = ResourceStatus.Approved
        };
        db.Resources.Add(resource);

        var cart = new Cart { UserId = BuyerId };
        cart.Items.Add(new CartItem
        {
            CartId = cart.Id,
            ItemType = CatalogItemType.Resource,
            ItemId = resource.Id,
            Price = resourcePrice
        });
        db.Carts.Add(cart);

        await db.SaveChangesAsync();
        return resource;
    }

    private static async Task<(Resource Resource, Training Training)> SeedWithTrainingAsync(ApplicationDbContext db)
    {
        var resource = await SeedAsync(db, resourcePrice: 10.00m);

        var training = new Training
        {
            Name = "Sinif idarəetməsi",
            Format = TrainingFormat.Video,
            Description = "Video kurs",
            Price = 45.00m,
            DurationHours = 6,
            MetaLabel = "18 dərs"
        };
        db.Trainings.Add(training);

        var cart = db.Carts.Single(c => c.UserId == BuyerId);
        db.CartItems.Add(new CartItem
        {
            CartId = cart.Id,
            ItemType = CatalogItemType.Training,
            ItemId = training.Id,
            Price = training.Price,
            AddedAt = DateTime.UtcNow.AddMinutes(1)
        });

        await db.SaveChangesAsync();
        return (resource, training);
    }
}
