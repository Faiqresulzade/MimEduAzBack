using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MimeduAz.Application.Common.Options;
using MimeduAz.Application.Services;

namespace MimeduAz.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<CommissionOptions>(configuration.GetSection(CommissionOptions.SectionName));
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
        services.Configure<RequestLogOptions>(configuration.GetSection(RequestLogOptions.SectionName));
        services.Configure<CertificateOptions>(configuration.GetSection(CertificateOptions.SectionName));

        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IResourceService, ResourceService>();
        services.AddScoped<ITrainingService, TrainingService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IQuizService, QuizService>();
        services.AddScoped<ICertificateService, CertificateService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IBlogService, BlogService>();

        return services;
    }
}
