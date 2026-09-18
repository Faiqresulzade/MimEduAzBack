using FluentAssertions;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Services;
using MimeduAz.Contracts.Trainings;
using MimeduAz.Domain.Entities;
using MimeduAz.Domain.Enums;
using MimeduAz.Infrastructure.Persistence;
using MimeduAz.Tests.TestSupport;

namespace MimeduAz.Tests;

/// <summary>Admin tərəfindən təlimin redaktəsi və silinməsi.</summary>
public sealed class TrainingAdminTests
{
    private static readonly Guid AdminId = Guid.NewGuid();
    private static readonly Guid StudentId = Guid.NewGuid();

    [Fact]
    public async Task Updating_a_training_replaces_its_fields_and_syllabus()
    {
        await using var db = TestHarness.CreateDb();
        var training = await SeedTrainingAsync(db);

        var updated = await CreateSut(db).UpdateAsync(training.Id, new UpdateTrainingRequest
        {
            Name = "Rəqəmsal qiymətləndirmə 2.0",
            Format = TrainingFormat.Online,
            Description = "Yenilənmiş proqram",
            Price = 75m,
            DurationHours = 12,
            MetaLabel = "3 gün · 12 saat",
            Syllabus = new List<string> { "Giriş", "Rubrikalar", "Praktika" }
        }, CancellationToken.None);

        updated.Name.Should().Be("Rəqəmsal qiymətləndirmə 2.0");
        updated.Price.Should().Be(75m);
        updated.DurationHours.Should().Be(12);
        updated.Syllabus.Select(s => s.Text)
            .Should().Equal("Giriş", "Rubrikalar", "Praktika");

        // Köhnə maddələr bazadan tamamilə silinməlidir, yalnız cavabdan yox.
        db.TrainingSyllabusItems.Count(s => s.TrainingId == training.Id).Should().Be(3);
    }

    [Fact]
    public async Task Switching_a_training_to_online_clears_the_seat_limit()
    {
        await using var db = TestHarness.CreateDb();
        var training = await SeedTrainingAsync(db, format: TrainingFormat.Live, seatLimit: 20);

        var updated = await CreateSut(db).UpdateAsync(
            training.Id, Request(TrainingFormat.Online, seatLimit: 20), CancellationToken.None);

        updated.SeatLimit.Should().BeNull();
        updated.SeatsLeft.Should().BeNull();
    }

    [Fact]
    public async Task Seat_limit_cannot_drop_below_the_number_of_people_already_enrolled()
    {
        await using var db = TestHarness.CreateDb();
        var training = await SeedTrainingAsync(db, format: TrainingFormat.Live, seatLimit: 20);
        await EnrollAsync(db, training);
        await EnrollAsync(db, training, Guid.NewGuid());

        var act = () => CreateSut(db).UpdateAsync(
            training.Id, Request(TrainingFormat.Live, seatLimit: 1), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Leaving_the_syllabus_out_keeps_the_existing_one()
    {
        await using var db = TestHarness.CreateDb();
        var training = await SeedTrainingAsync(db);

        var updated = await CreateSut(db).UpdateAsync(
            training.Id, Request(TrainingFormat.Online), CancellationToken.None);

        updated.Syllabus.Should().HaveCount(2);
    }

    [Fact]
    public async Task An_empty_training_can_be_deleted_together_with_its_lessons()
    {
        await using var db = TestHarness.CreateDb();
        var training = await SeedTrainingAsync(db);

        await CreateSut(db).DeleteAsync(training.Id, CancellationToken.None);

        db.Trainings.Any(t => t.Id == training.Id).Should().BeFalse();
        db.TrainingLessons.Any(l => l.TrainingId == training.Id).Should().BeFalse();
    }

    [Fact]
    public async Task Deleting_a_training_that_has_enrollments_is_rejected_with_a_conflict()
    {
        await using var db = TestHarness.CreateDb();
        var training = await SeedTrainingAsync(db);
        await EnrollAsync(db, training);

        var act = () => CreateSut(db).DeleteAsync(training.Id, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        db.Trainings.Any(t => t.Id == training.Id).Should().BeTrue();
    }

    [Fact]
    public async Task Deleting_a_training_also_clears_it_from_open_carts()
    {
        await using var db = TestHarness.CreateDb();
        var training = await SeedTrainingAsync(db);

        var cart = new Cart { UserId = StudentId };
        db.Carts.Add(cart);
        db.CartItems.Add(new CartItem
        {
            CartId = cart.Id,
            ItemType = CatalogItemType.Training,
            ItemId = training.Id,
            Price = training.Price
        });
        await db.SaveChangesAsync();

        await CreateSut(db).DeleteAsync(training.Id, CancellationToken.None);

        db.CartItems.Any(i => i.ItemId == training.Id).Should().BeFalse();
    }

    [Fact]
    public async Task Updating_a_missing_training_reports_not_found()
    {
        await using var db = TestHarness.CreateDb();

        var act = () => CreateSut(db).UpdateAsync(
            Guid.NewGuid(), Request(TrainingFormat.Online), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static UpdateTrainingRequest Request(TrainingFormat format, int? seatLimit = null) => new()
    {
        Name = "Rəqəmsal qiymətləndirmə",
        Format = format,
        Description = "Təsvir",
        Price = 60m,
        DurationHours = 8,
        MetaLabel = "2 gün · 8 saat",
        SeatLimit = seatLimit
    };

    private static TrainingService CreateSut(ApplicationDbContext db) => new(
        db,
        new FakeCurrentUserService(AdminId, isAdmin: true),
        new CertificateService(
            db,
            new FakeCurrentUserService(AdminId, isAdmin: true),
            new FakeCertificateDocumentService(),
            TestHarness.Logger<CertificateService>()),
        TestHarness.Logger<TrainingService>());

    private static async Task<Training> SeedTrainingAsync(
        ApplicationDbContext db,
        TrainingFormat format = TrainingFormat.Online,
        int? seatLimit = null)
    {
        var training = new Training
        {
            Name = "Rəqəmsal qiymətləndirmə",
            Format = format,
            Description = "Köhnə təsvir",
            Price = 60m,
            DurationHours = 8,
            MetaLabel = "2 gün · 8 saat",
            SeatLimit = seatLimit
        };

        training.SyllabusItems.Add(new TrainingSyllabusItem
        {
            TrainingId = training.Id, OrderIndex = 0, Text = "Köhnə maddə 1"
        });
        training.SyllabusItems.Add(new TrainingSyllabusItem
        {
            TrainingId = training.Id, OrderIndex = 1, Text = "Köhnə maddə 2"
        });
        training.Lessons.Add(new TrainingLesson
        {
            TrainingId = training.Id, OrderIndex = 0, Title = "Dərs 1", Description = "Təsvir"
        });

        db.Trainings.Add(training);
        await db.SaveChangesAsync();
        return training;
    }

    private static async Task EnrollAsync(ApplicationDbContext db, Training training, Guid? userId = null)
    {
        var id = userId ?? StudentId;

        db.Users.Add(new ApplicationUser { Id = id, FullName = "Aysel Məmmədova", Email = $"{id:N}@mimedu.az" });
        db.Enrollments.Add(new Enrollment { UserId = id, TrainingId = training.Id });
        await db.SaveChangesAsync();
    }
}
