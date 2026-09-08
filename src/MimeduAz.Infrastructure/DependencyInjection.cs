using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Options;
using MimeduAz.Domain.Entities;
using MimeduAz.Infrastructure.Certificates;
using MimeduAz.Infrastructure.Identity;
using MimeduAz.Infrastructure.Logging;
using MimeduAz.Infrastructure.Persistence;
using MimeduAz.Infrastructure.Storage;

namespace MimeduAz.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string webRootPath)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "\"ConnectionStrings:DefaultConnection\" konfiqurasiyası tapılmadı.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);

                // Supabase-in transaction pooler-i (port 6543) qısa müddətli kəsilmələr verə bilir.
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            }));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

        // Sertifikat renderi vəziyyət saxlamır - singleton kifayətdir.
        services.AddSingleton<ICertificateDocumentService, CertificateDocumentService>();

        services.AddScoped<IFileStorageService>(sp => new LocalFileStorageService(
            sp.GetRequiredService<IOptions<FileStorageOptions>>(),
            webRootPath,
            sp.GetRequiredService<ILogger<LocalFileStorageService>>()));

        services.AddScoped<DataSeeder>();

        // Audit log: növbə singleton-dur, bazaya yazan servis arxa planda işləyir.
        services.AddSingleton<RequestLogQueue>();
        services.AddSingleton<IRequestLogSink>(sp => sp.GetRequiredService<RequestLogQueue>());
        services.AddHostedService<RequestLogWriter>();

        return services;
    }
}
