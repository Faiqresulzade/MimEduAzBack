using FluentAssertions;
using Microsoft.Extensions.Options;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Options;
using MimeduAz.Application.Services;
using MimeduAz.Contracts.Orders;
using MimeduAz.Contracts.Payouts;
using MimeduAz.Domain.Entities;
using MimeduAz.Domain.Enums;
using MimeduAz.Infrastructure.Persistence;
using MimeduAz.Tests.TestSupport;

namespace MimeduAz.Tests;

/// <summary>
/// Checkout və AzeriCard ödəniş axını: giriş yalnız bankın imzalı təsdiqi və tamamlamadan sonra açılır.
/// </summary>
public sealed class OrderServiceTests
{
    private static readonly Guid BuyerId = Guid.NewGuid();
    private static readonly Guid AuthorId = Guid.NewGuid();
    private const string Terminal = "17205356";

    private readonly FakePaymentGateway _gateway = new();
    private readonly FakeEmailQueue _emails = new();

    // ------------------------------------------------------------ Komissiya

    [Fact]
    public async Task Checkout_paid_resource_splits_20_percent_commission_and_80_percent_to_author()
    {
        await using var db = TestHarness.CreateDb();
        var resource = await SeedAsync(db, resourcePrice: 10.00m);

        var result = await CreateSut(db).CheckoutAsync(Paid(), CancellationToken.None);

        result.Order.TotalAmount.Should().Be(10.00m);
        var line = db.OrderItems.Single(i => i.ItemId == resource.Id);
        line.CommissionAmount.Should().Be(2.00m);
        line.AuthorPayoutAmount.Should().Be(8.00m);
    }

    [Fact]
    public async Task Checkout_rounds_commission_to_two_decimals()
    {
        await using var db = TestHarness.CreateDb();
        await SeedAsync(db, resourcePrice: 8.55m);

        var result = await CreateSut(db).CheckoutAsync(Paid(), CancellationToken.None);

        var line = db.OrderItems.Single();
        line.CommissionAmount.Should().Be(1.71m);
        line.AuthorPayoutAmount.Should().Be(6.84m);
        (line.CommissionAmount + line.AuthorPayoutAmount).Should().Be(result.Order.TotalAmount);
    }

    // ------------------------------------------------------------ Checkout

    [Fact]
    public async Task Paid_checkout_creates_pending_order_with_bank_form_and_grants_nothing()
    {
        await using var db = TestHarness.CreateDb();
        var (_, training) = await SeedWithTrainingAsync(db);

        var result = await CreateSut(db).CheckoutAsync(Paid(), CancellationToken.None);

        result.Order.Status.Should().Be(OrderStatus.Pending);
        result.Payment.Should().NotBeNull();
        result.Payment!.Fields["ORDER"].Should().MatchRegex("^[0-9]{16}$");
        db.Orders.Single().TermsAcceptedAt.Should().NotBeNull();

        // Ödəniş təsdiqlənənə qədər nə giriş, nə də səbətin boşalması.
        db.Enrollments.Should().NotContain(e => e.TrainingId == training.Id);
        db.CartItems.Should().HaveCount(2);
    }

    [Fact]
    public async Task Paid_checkout_is_rejected_while_card_payments_are_disabled()
    {
        await using var db = TestHarness.CreateDb();
        await SeedAsync(db, resourcePrice: 5.00m);

        var act = () => CreateSut(db, enabled: false).CheckoutAsync(Paid(), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*tezliklə*");
        db.Orders.Should().BeEmpty();
        db.CartItems.Should().ContainSingle();
    }

    [Fact]
    public async Task Paid_checkout_requires_terms_acceptance()
    {
        await using var db = TestHarness.CreateDb();
        await SeedAsync(db, resourcePrice: 5.00m);

        var act = () => CreateSut(db).CheckoutAsync(new CheckoutRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationFailedException>();
        db.Orders.Should().BeEmpty();
    }

    [Fact]
    public async Task Free_cart_completes_immediately_even_when_payments_are_disabled()
    {
        await using var db = TestHarness.CreateDb();
        var training = await SeedFreeTrainingAsync(db);

        var result = await CreateSut(db, enabled: false).CheckoutAsync(new CheckoutRequest(), CancellationToken.None);

        result.Order.Status.Should().Be(OrderStatus.Paid);
        result.Payment.Should().BeNull();
        db.Enrollments.Should().ContainSingle(e => e.TrainingId == training.Id && e.UserId == BuyerId);
        db.CartItems.Should().BeEmpty();
    }

    [Fact]
    public async Task Checkout_with_empty_cart_throws()
    {
        await using var db = TestHarness.CreateDb();
        db.Users.Add(new ApplicationUser { Id = BuyerId, FullName = "Alıcı Müəllim", Email = "b@mimedu.az" });
        await db.SaveChangesAsync();

        var act = () => CreateSut(db).CheckoutAsync(Paid(), CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*Səbət boşdur*");
    }

    // ------------------------------------------------------------ Bank callback

    [Fact]
    public async Task Approved_signed_callback_completes_payment_grants_access_and_sends_receipt()
    {
        await using var db = TestHarness.CreateDb();
        var (_, training) = await SeedWithTrainingAsync(db);
        var order = (await CreateSut(db).CheckoutAsync(Paid(), CancellationToken.None)).Order;

        var outcome = await Payments(db).HandleCallbackAsync(Callback(db, amount: "55.00"), CancellationToken.None);

        outcome.Status.Should().Be(OrderStatus.Paid);
        var saved = db.Orders.Single();
        saved.Rrn.Should().Be("306276834930");
        saved.PaidAt.Should().NotBeNull();
        db.Enrollments.Should().ContainSingle(e => e.TrainingId == training.Id);
        db.CartItems.Should().BeEmpty();
        _gateway.Completions.Should().Be(1);
        _emails.Sent.Should().ContainSingle(m => m.Subject.Contains(order.Code));
    }

    [Fact]
    public async Task Repeated_callback_changes_nothing()
    {
        await using var db = TestHarness.CreateDb();
        await SeedWithTrainingAsync(db);
        await CreateSut(db).CheckoutAsync(Paid(), CancellationToken.None);

        await Payments(db).HandleCallbackAsync(Callback(db, amount: "55.00"), CancellationToken.None);
        await Payments(db).HandleCallbackAsync(Callback(db, amount: "55.00"), CancellationToken.None);

        db.Enrollments.Should().ContainSingle();
        _gateway.Completions.Should().Be(1);
        _emails.Sent.Should().ContainSingle();
    }

    [Theory]
    [InlineData(false, "10.00", Terminal)]   // imza səhvdir
    [InlineData(true, "1.00", Terminal)]     // məbləğ dəyişdirilib
    [InlineData(true, "10.00", "99999999")]  // başqa terminal
    public async Task Invalid_callback_does_not_grant_access(bool signatureValid, string amount, string terminal)
    {
        await using var db = TestHarness.CreateDb();
        await SeedAsync(db, resourcePrice: 10.00m);
        await CreateSut(db).CheckoutAsync(Paid(), CancellationToken.None);

        var outcome = await Payments(db).HandleCallbackAsync(
            Callback(db, amount, terminal: terminal, signatureValid: signatureValid), CancellationToken.None);

        outcome.Status.Should().Be(OrderStatus.Pending);
        db.Orders.Single().PaymentResult.Should().StartWith("Rədd edildi");
        _gateway.Completions.Should().Be(0);
    }

    [Fact]
    public async Task Bank_transaction_cannot_pay_two_orders()
    {
        await using var db = TestHarness.CreateDb();
        await SeedAsync(db, resourcePrice: 10.00m);
        var sut = CreateSut(db);
        await sut.CheckoutAsync(Paid(), CancellationToken.None);
        var first = db.Orders.Single();
        await Payments(db).HandleCallbackAsync(Callback(db, "10.00"), CancellationToken.None);

        // Eyni məbləğli ikinci sifariş - köhnə (real, imzalı) callback təkrar göndərilir.
        db.CartItems.Add(new CartItem
        {
            CartId = db.Carts.Single().Id,
            ItemType = CatalogItemType.Resource,
            ItemId = db.Resources.Single().Id,
            Price = 10.00m
        });
        await db.SaveChangesAsync();
        await sut.CheckoutAsync(Paid(), CancellationToken.None);
        var second = db.Orders.Single(o => o.Id != first.Id);

        var replay = Callback(db, "10.00");
        replay["ORDER"] = second.PaymentNumber!;
        var outcome = await Payments(db).HandleCallbackAsync(replay, CancellationToken.None);

        outcome.Status.Should().Be(OrderStatus.Pending);
        db.Orders.Single(o => o.Id == second.Id).PaymentResult.Should().Contain("başqa sifariş");
    }

    [Fact]
    public async Task Declined_callback_marks_order_failed()
    {
        await using var db = TestHarness.CreateDb();
        await SeedAsync(db, resourcePrice: 10.00m);
        await CreateSut(db).CheckoutAsync(Paid(), CancellationToken.None);

        var declined = Callback(db, "10.00");
        declined["ACTION"] = "2";
        declined["RC"] = "05";
        var outcome = await Payments(db).HandleCallbackAsync(declined, CancellationToken.None);

        outcome.Status.Should().Be(OrderStatus.Failed);
        db.CartItems.Should().ContainSingle();
    }

    [Fact]
    public async Task Failed_completion_keeps_order_pending_and_worker_retries_it()
    {
        await using var db = TestHarness.CreateDb();
        await SeedAsync(db, resourcePrice: 10.00m);
        await CreateSut(db).CheckoutAsync(Paid(), CancellationToken.None);

        _gateway.CompletionAction = "3";
        await Payments(db).HandleCallbackAsync(Callback(db, "10.00"), CancellationToken.None);
        db.Orders.Single().Status.Should().Be(OrderStatus.Pending);

        // Fon işi yalnız 15 dəqiqədən köhnə sifarişlərə baxır.
        db.Orders.Single().CreatedAt = DateTime.UtcNow.AddMinutes(-20);
        await db.SaveChangesAsync();
        _gateway.CompletionAction = "0";
        await Payments(db).ProcessPendingAsync(CancellationToken.None);

        db.Orders.Single().Status.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public async Task Unpaid_order_older_than_a_day_is_cancelled()
    {
        await using var db = TestHarness.CreateDb();
        await SeedAsync(db, resourcePrice: 10.00m);
        await CreateSut(db).CheckoutAsync(Paid(), CancellationToken.None);
        db.Orders.Single().CreatedAt = DateTime.UtcNow.AddHours(-25);
        await db.SaveChangesAsync();

        await Payments(db).ProcessPendingAsync(CancellationToken.None);

        db.Orders.Single().Status.Should().Be(OrderStatus.Cancelled);
    }

    // ------------------------------------------------------------ Qaytarma və müəllif payı

    [Fact]
    public async Task Refund_revokes_access_and_removes_author_earnings()
    {
        await using var db = TestHarness.CreateDb();
        var (_, training) = await SeedWithTrainingAsync(db);
        await CreateSut(db).CheckoutAsync(Paid(), CancellationToken.None);
        await Payments(db).HandleCallbackAsync(Callback(db, "55.00"), CancellationToken.None);

        var payouts = new PayoutService(db, new FakeCurrentUserService(AuthorId), TestHarness.Logger<PayoutService>());
        (await payouts.GetMyEarningsAsync(CancellationToken.None)).Net.Should().Be(8.00m);

        var refunded = await Payments(db).RefundAsync(db.Orders.Single().Id, CancellationToken.None);

        refunded.Status.Should().Be(OrderStatus.Refunded);
        db.Enrollments.Should().NotContain(e => e.TrainingId == training.Id);
        (await payouts.GetMyEarningsAsync(CancellationToken.None)).Net.Should().Be(0m);
    }

    [Fact]
    public async Task Payout_requires_minimum_balance_and_blocks_a_second_pending_request()
    {
        await using var db = TestHarness.CreateDb();
        await SeedAsync(db, resourcePrice: 30.00m); // müəllifə 24 ₼
        await CreateSut(db).CheckoutAsync(Paid(), CancellationToken.None);
        await Payments(db).HandleCallbackAsync(Callback(db, "30.00"), CancellationToken.None);

        var payouts = new PayoutService(db, new FakeCurrentUserService(AuthorId), TestHarness.Logger<PayoutService>());
        var request = new CreatePayoutRequest
        {
            Iban = "AZ21 NABZ 0000 0000 1370 1000 1944",
            AccountHolder = "Müəllif Müəllim",
            Fin = "5abc12d"
        };

        var payout = await payouts.RequestAsync(request, CancellationToken.None);
        payout.Amount.Should().Be(24.00m);
        payout.IbanMasked.Should().Be("AZ21 **** **** 1944");
        db.PayoutRequests.Single().Fin.Should().Be("5ABC12D");

        var again = () => payouts.RequestAsync(request, CancellationToken.None);
        await again.Should().ThrowAsync<ConflictException>();

        (await payouts.GetMyEarningsAsync(CancellationToken.None)).Available.Should().Be(0m);
    }

    // ------------------------------------------------------------ Köməkçilər

    private static CheckoutRequest Paid() => new() { AcceptTerms = true };

    private OrderService CreateSut(ApplicationDbContext db, bool enabled = true)
    {
        _gateway.Configured = enabled;
        return new OrderService(
            db,
            new FakeCurrentUserService(BuyerId),
            TestHarness.Commission(),
            Options.Create(new PaymentOptions { Enabled = enabled }),
            _gateway,
            Fulfillment(db),
            TestHarness.Logger<OrderService>());
    }

    private PaymentService Payments(ApplicationDbContext db) => new(
        db,
        _gateway,
        Fulfillment(db),
        Options.Create(new PaymentOptions { Enabled = true }),
        Options.Create(new AzeriCardOptions { Terminal = Terminal }),
        TestHarness.Logger<PaymentService>());

    private OrderFulfillment Fulfillment(ApplicationDbContext db) => new(
        db,
        _emails,
        Options.Create(new SellerOptions { Name = "Satıcı", Voen = "4002166852" }),
        Options.Create(new PaymentOptions()),
        TestHarness.Logger<OrderFulfillment>());

    /// <summary>Son Pending sifariş üçün bankın təsdiq callback-i.</summary>
    private static Dictionary<string, string> Callback(
        ApplicationDbContext db, string amount, string terminal = Terminal, bool signatureValid = true) => new()
    {
        ["ORDER"] = db.Orders.OrderByDescending(o => o.CreatedAt).First().PaymentNumber!,
        ["AMOUNT"] = amount,
        ["CURRENCY"] = "944",
        ["TERMINAL"] = terminal,
        ["ACTION"] = "0",
        ["RC"] = "00",
        ["RRN"] = "306276834930",
        ["INT_REF"] = "4A29E93C607E33DC",
        ["APPROVAL"] = "168975",
        [FakePaymentGateway.SignatureField] = signatureValid ? "ok" : "bad"
    };

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
        var training = await AddTrainingToCartAsync(db, price: 45.00m);
        return (resource, training);
    }

    private static async Task<Training> SeedFreeTrainingAsync(ApplicationDbContext db)
    {
        db.Users.Add(new ApplicationUser { Id = BuyerId, FullName = "Alıcı Müəllim", Email = "b@mimedu.az" });
        db.Carts.Add(new Cart { UserId = BuyerId });
        await db.SaveChangesAsync();
        return await AddTrainingToCartAsync(db, price: 0m);
    }

    private static async Task<Training> AddTrainingToCartAsync(ApplicationDbContext db, decimal price)
    {
        var training = new Training
        {
            Name = "Sinif idarəetməsi",
            Format = TrainingFormat.Video,
            Description = "Video kurs",
            Price = price,
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
            Price = price,
            AddedAt = DateTime.UtcNow.AddMinutes(1)
        });

        await db.SaveChangesAsync();
        return training;
    }
}

/// <summary>
/// Bankı simulyasiya edən gateway. İmzanın düzgünlüyü callback-dəki xüsusi sahə ilə idarə olunur;
/// real imza yoxlaması <see cref="AzeriCardSignerTests"/>-də sınanır.
/// </summary>
public sealed class FakePaymentGateway : IPaymentGateway
{
    public const string SignatureField = "TEST_SIGNATURE";

    public bool Configured { get; set; } = true;
    public string CompletionAction { get; set; } = "0";
    public int Completions { get; private set; }

    public bool IsConfigured => Configured;

    public PaymentRedirect CreateSale(Order order, string description, string? email, string? clientInfo) =>
        new("https://testmpi.3dsecure.az/cgi-bin/cgi_link", new Dictionary<string, string>
        {
            ["ORDER"] = order.PaymentNumber!,
            ["AMOUNT"] = order.TotalAmount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            ["P_SIGN"] = "imza"
        });

    public PaymentResult ParseCallback(IReadOnlyDictionary<string, string> fields) => new(
        fields.GetValueOrDefault(SignatureField) == "ok",
        fields.GetValueOrDefault("ORDER"),
        fields.GetValueOrDefault("AMOUNT"),
        fields.GetValueOrDefault("CURRENCY"),
        fields.GetValueOrDefault("TERMINAL"),
        fields.GetValueOrDefault("ACTION"),
        fields.GetValueOrDefault("RC"),
        fields.GetValueOrDefault("RRN"),
        fields.GetValueOrDefault("INT_REF"),
        fields.GetValueOrDefault("APPROVAL"),
        "412720******2601",
        fields);

    public Task<PaymentResult> CompleteAsync(Order order, CancellationToken ct)
    {
        if (CompletionAction == "0")
        {
            Completions++;
        }

        return Task.FromResult(Result(CompletionAction));
    }

    public Task<PaymentResult> QueryStatusAsync(Order order, CancellationToken ct) => Task.FromResult(Result("8"));

    public Task<PaymentResult> RefundAsync(Order order, CancellationToken ct) => Task.FromResult(Result("0"));

    private static PaymentResult Result(string action) =>
        new(true, null, null, null, null, action, action == "0" ? "00" : "-1", null, null, null, null,
            new Dictionary<string, string> { ["ACTION"] = action });
}

public sealed class FakeEmailQueue : IEmailQueue
{
    public List<EmailMessage> Sent { get; } = new();

    public bool TryEnqueue(EmailMessage message)
    {
        Sent.Add(message);
        return true;
    }
}
