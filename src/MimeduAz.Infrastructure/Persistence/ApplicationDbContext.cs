using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Domain.Entities;

namespace MimeduAz.Infrastructure.Persistence;

public sealed class ApplicationDbContext
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<Training> Trainings => Set<Training>();
    public DbSet<TrainingSyllabusItem> TrainingSyllabusItems => Set<TrainingSyllabusItem>();
    public DbSet<TrainingLesson> TrainingLessons => Set<TrainingLesson>();
    public DbSet<LessonCompletion> LessonCompletions => Set<LessonCompletion>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
    public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<BlogPost> BlogPosts => Set<BlogPost>();
    public DbSet<RequestLog> RequestLogs => Set<RequestLog>();

    // IApplicationDbContext, IdentityDbContext-in Users/Roles/UserRoles xassələrini yenidən elan etmir -
    // baza sinfindəki DbSet-lər interfeysi olduğu kimi qarşılayır.
    DbSet<IdentityUserRole<Guid>> IApplicationDbContext.UserRoles => UserRoles;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Identity cədvəllərinə daha oxunaqlı adlar veririk.
        builder.Entity<ApplicationUser>().ToTable("users");
        builder.Entity<ApplicationRole>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
    }
}
