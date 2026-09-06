using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MimeduAz.Domain.Entities;

namespace MimeduAz.Application.Common.Interfaces;

/// <summary>
/// Application qatının məlumat bazasına yeganə çıxışı.
/// Konkret <c>ApplicationDbContext</c> Infrastructure qatındadır — beləliklə
/// biznes məntiq EF Core provayderindən asılı qalmır və test edilə bilir.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<ApplicationUser> Users { get; }
    DbSet<ApplicationRole> Roles { get; }
    DbSet<IdentityUserRole<Guid>> UserRoles { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Resource> Resources { get; }
    DbSet<Training> Trainings { get; }
    DbSet<TrainingSyllabusItem> TrainingSyllabusItems { get; }
    DbSet<Enrollment> Enrollments { get; }
    DbSet<Cart> Carts { get; }
    DbSet<CartItem> CartItems { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<Quiz> Quizzes { get; }
    DbSet<QuizQuestion> QuizQuestions { get; }
    DbSet<QuizAttempt> QuizAttempts { get; }
    DbSet<Certificate> Certificates { get; }
    DbSet<BlogPost> BlogPosts { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
