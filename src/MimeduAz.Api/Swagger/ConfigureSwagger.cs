using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MimeduAz.Api.Swagger;

public static class SwaggerSetup
{
    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "MIMEDU.AZ API",
                Version = "v1",
                Description =
                    "Azərbaycan müəllimləri üçün rəqəmsal təhsil platformasının API-si. " +
                    "Ödəniş axını demo rejimdədir - heç bir kart məlumatı qəbul edilmir və saxlanılmır."
            });

            var jwtScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Access token-i \"Bearer\" prefiksi olmadan daxil edin.",
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            };

            options.AddSecurityDefinition("Bearer", jwtScheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [jwtScheme] = Array.Empty<string>()
            });

            var xmlFile = $"{typeof(SwaggerSetup).Assembly.GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }

            options.OperationFilter<FileUploadOperationFilter>();
        });

        return services;
    }
}

/// <summary>multipart/form-data qəbul edən endpoint-lərdə fayl sahəsini Swagger UI-də düzgün göstərir.</summary>
public sealed class FileUploadOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasFormFile = context.MethodInfo
            .GetParameters()
            .Any(p => p.ParameterType == typeof(IFormFile));

        if (!hasFormFile)
        {
            return;
        }

        foreach (var content in operation.RequestBody?.Content ?? new Dictionary<string, OpenApiMediaType>())
        {
            if (content.Key != "multipart/form-data")
            {
                continue;
            }

            if (content.Value.Schema.Properties.TryGetValue("file", out var fileSchema))
            {
                fileSchema.Type = "string";
                fileSchema.Format = "binary";
            }
        }
    }
}
