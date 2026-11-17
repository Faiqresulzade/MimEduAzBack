using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Options;
using MimeduAz.Application.Services;
using MimeduAz.Contracts.Auth;
using MimeduAz.Domain.Constants;
using MimeduAz.Domain.Entities;
using MimeduAz.Domain.Enums;
using MimeduAz.Infrastructure.Identity;
using MimeduAz.Infrastructure.Persistence;
using MimeduAz.Tests.TestSupport;

namespace MimeduAz.Tests;

public sealed class GoogleAuthTests : IAsyncLifetime
{
    private const string ClientId = "test-client.apps.googleusercontent.com";

    private ServiceProvider _services = null!;
    private ApplicationDbContext _db = null!;
    private UserManager<ApplicationUser> _users = null!;
    private readonly FakeGoogleValidator _google = new();

    public async Task InitializeAsync()
    {
        var dbName = $"google-auth-{Guid.NewGuid()}";
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(dbName));
        collection
            .AddIdentityCore<ApplicationUser>(o => o.User.RequireUniqueEmail = true)
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        _services = collection.BuildServiceProvider();
        _db = _services.GetRequiredService<ApplicationDbContext>();
        _users = _services.GetRequiredService<UserManager<ApplicationUser>>();

        var roles = _services.GetRequiredService<RoleManager<ApplicationRole>>();
        foreach (var role in AppRoles.All)
        {
            await roles.CreateAsync(new ApplicationRole { Name = role });
        }
    }

    public async Task DisposeAsync() => await _services.DisposeAsync();

    [Fact]
    public async Task Existing_password_account_with_same_email_is_linked_and_logged_in()
    {
        var existing = new ApplicationUser { UserName = "lale@gmail.com", Email = "lale@gmail.com", FullName = "Lalə" };
        (await _users.CreateAsync(existing, "Sifre1234!")).Succeeded.Should().BeTrue();
        await _users.AddToRoleAsync(existing, AppRoles.Teacher);
        _google.Next = new GoogleIdentity("google-sub-1", "lale@gmail.com", true, "Lalə Həsənova");

        var result = await Sut().GoogleLoginAsync(new GoogleLoginRequest("token"), CancellationToken.None);

        result.RequiresAccountType.Should().BeFalse();
        result.Auth!.User.Id.Should().Be(existing.Id);
        result.Auth.User.Roles.Should().Contain(AppRoles.Teacher);
        _db.Users.Should().ContainSingle();
        (await _users.GetLoginsAsync(existing)).Should().ContainSingle(l => l.ProviderKey == "google-sub-1");

        // Şifrə ilə giriş də işləməyə davam edir.
        var password = await Sut().LoginAsync(new LoginRequest("lale@gmail.com", "Sifre1234!"), CancellationToken.None);
        password.User.Id.Should().Be(existing.Id);
    }

    [Fact]
    public async Task New_user_without_account_type_is_asked_and_not_created()
    {
        _google.Next = new GoogleIdentity("google-sub-2", "yeni@gmail.com", true, "Yeni Müəllim");

        var result = await Sut().GoogleLoginAsync(new GoogleLoginRequest("token"), CancellationToken.None);

        result.RequiresAccountType.Should().BeTrue();
        result.Auth.Should().BeNull();
        result.FullName.Should().Be("Yeni Müəllim");
        _db.Users.Should().BeEmpty();
    }

    [Fact]
    public async Task New_user_with_teacher_type_is_created_without_password()
    {
        _google.Next = new GoogleIdentity("google-sub-3", "muellim@gmail.com", true, "Gülüm Bəşirova");

        var result = await Sut().GoogleLoginAsync(
            new GoogleLoginRequest("token", AccountType.Teacher, AcceptTerms: true), CancellationToken.None);

        result.Auth!.User.FullName.Should().Be("Gülüm Bəşirova");
        result.Auth.User.Roles.Should().Equal(AppRoles.Teacher);
        result.Auth.User.CanPublishResources.Should().BeTrue();

        var user = await _users.FindByEmailAsync("muellim@gmail.com");
        user!.EmailConfirmed.Should().BeTrue();
        (await _users.HasPasswordAsync(user)).Should().BeFalse();
        user.TermsAcceptedAt.Should().NotBeNull();
        user.TermsVersion.Should().Be("2026-11-17");

        // İkinci dəfə Google ilə - eyni hesab, sual verilmir.
        var again = await Sut().GoogleLoginAsync(new GoogleLoginRequest("token"), CancellationToken.None);
        again.RequiresAccountType.Should().BeFalse();
        again.Auth!.User.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task New_user_is_not_created_without_accepting_terms()
    {
        _google.Next = new GoogleIdentity("google-sub-6", "razi@gmail.com", true, "Razı Deyil");

        var act = () => Sut().GoogleLoginAsync(
            new GoogleLoginRequest("token", AccountType.Teacher), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationFailedException>();
        _db.Users.Should().BeEmpty();
    }

    [Fact]
    public async Task Password_login_to_google_only_account_explains_how_to_sign_in()
    {
        _google.Next = new GoogleIdentity("google-sub-4", "g@gmail.com", true, "G");
        await Sut().GoogleLoginAsync(new GoogleLoginRequest("token", AccountType.Student, AcceptTerms: true), CancellationToken.None);

        var act = () => Sut().LoginAsync(new LoginRequest("g@gmail.com", "nəsə123"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>().WithMessage("*Google ilə açılıb*");
    }

    [Fact]
    public async Task Invalid_token_and_unverified_email_are_rejected()
    {
        _google.Next = null;
        var invalid = () => Sut().GoogleLoginAsync(new GoogleLoginRequest("bad"), CancellationToken.None);
        await invalid.Should().ThrowAsync<UnauthorizedException>();

        _google.Next = new GoogleIdentity("google-sub-5", "x@gmail.com", false, "X");
        var unverified = () => Sut().GoogleLoginAsync(new GoogleLoginRequest("token"), CancellationToken.None);
        await unverified.Should().ThrowAsync<UnauthorizedException>().WithMessage("*təsdiqlənməyib*");

        _db.Users.Should().BeEmpty();
    }

    [Fact]
    public async Task Disabled_when_client_id_is_missing()
    {
        var sut = Sut(clientId: null);

        sut.GetGoogleConfig().ClientId.Should().BeNull();
        var act = () => sut.GoogleLoginAsync(new GoogleLoginRequest("token"), CancellationToken.None);
        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    [Fact]
    public async Task Deleting_account_removes_personal_data_and_blocks_all_logins()
    {
        var user = new ApplicationUser { UserName = "sil@gmail.com", Email = "sil@gmail.com", FullName = "Silinən", Subject = "Kimya" };
        (await _users.CreateAsync(user, "Sifre1234!")).Succeeded.Should().BeTrue();
        await _users.AddToRoleAsync(user, AppRoles.Teacher);
        await _users.AddLoginAsync(user, new UserLoginInfo("Google", "google-sub-del", "Google"));
        var login = await Sut().LoginAsync(new LoginRequest("sil@gmail.com", "Sifre1234!"), CancellationToken.None);

        await Sut(userId: user.Id).DeleteCurrentAccountAsync(CancellationToken.None);

        var stored = await _db.Users.SingleAsync(u => u.Id == user.Id);
        stored.FullName.Should().Be("Silinmiş istifadəçi");
        stored.Email.Should().NotContain("sil@gmail.com");
        stored.Subject.Should().BeNull();
        stored.PasswordHash.Should().BeNull();
        (await _users.GetLoginsAsync(stored)).Should().BeEmpty();

        var password = () => Sut().LoginAsync(new LoginRequest("sil@gmail.com", "Sifre1234!"), CancellationToken.None);
        await password.Should().ThrowAsync<UnauthorizedException>();
        var refresh = () => Sut().RefreshAsync(new RefreshTokenRequest(login.RefreshToken), CancellationToken.None);
        await refresh.Should().ThrowAsync<UnauthorizedException>();

        // Eyni e-poçtla yenidən qeydiyyat mümkündür (yeni, boş hesab).
        var again = await Sut().RegisterAsync(
            new RegisterRequest("Yeni", "sil@gmail.com", "Sifre1234!", null, AccountType.Student), CancellationToken.None);
        again.User.Id.Should().NotBe(user.Id);
    }

    [Fact]
    public async Task Admin_account_cannot_be_deleted_from_app()
    {
        var admin = new ApplicationUser { UserName = "a@x.az", Email = "a@x.az", FullName = "Admin" };
        (await _users.CreateAsync(admin, "Sifre1234!")).Succeeded.Should().BeTrue();
        await _users.AddToRoleAsync(admin, AppRoles.Admin);

        var act = () => Sut(userId: admin.Id).DeleteCurrentAccountAsync(CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>();
    }

    private AuthService Sut(string? clientId = ClientId, Guid? userId = null)
    {
        var jwt = Options.Create(new JwtOptions
        {
            Issuer = "test", Audience = "test", SecretKey = new string('k', 48), AccessTokenExpiryMinutes = 15
        });

        return new AuthService(
            _users,
            _db,
            new JwtTokenGenerator(jwt),
            new FakeCurrentUserService(userId),
            _google,
            jwt,
            Options.Create(new GoogleAuthOptions { ClientId = clientId }),
            Options.Create(new PaymentOptions { TermsVersion = "2026-11-17" }),
            NullLogger<AuthService>.Instance);
    }

    private sealed class FakeGoogleValidator : IGoogleTokenValidator
    {
        public GoogleIdentity? Next { get; set; }

        public Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken ct) => Task.FromResult(Next);
    }
}
