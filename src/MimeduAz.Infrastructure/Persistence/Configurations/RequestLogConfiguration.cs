using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MimeduAz.Domain.Entities;

namespace MimeduAz.Infrastructure.Persistence.Configurations;

public sealed class RequestLogConfiguration : IEntityTypeConfiguration<RequestLog>
{
    public void Configure(EntityTypeBuilder<RequestLog> builder)
    {
        builder.ToTable("request_logs");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).ValueGeneratedOnAdd();

        builder.Property(l => l.TraceId).IsRequired().HasMaxLength(100);
        builder.Property(l => l.Method).IsRequired().HasMaxLength(10);
        builder.Property(l => l.Path).IsRequired().HasMaxLength(500);
        builder.Property(l => l.QueryString).HasMaxLength(1000);
        builder.Property(l => l.UserEmail).HasMaxLength(256);
        builder.Property(l => l.IpAddress).HasMaxLength(64);
        builder.Property(l => l.UserAgent).HasMaxLength(512);
        builder.Property(l => l.RequestContentType).HasMaxLength(150);
        builder.Property(l => l.ExceptionType).HasMaxLength(200);

        // Gövdələr uzun ola bilər - MaxLength qoymuruq ki, MySQL longtext kimi map olunsun.
        builder.Property(l => l.RequestBody);
        builder.Property(l => l.ResponseBody);

        // Admin panelindəki tipik filtrlər: tarix, status, istifadəçi, yol.
        builder.HasIndex(l => l.CreatedAt);
        builder.HasIndex(l => l.StatusCode);
        builder.HasIndex(l => l.UserId);
        builder.HasIndex(l => new { l.Method, l.Path });

        // İstifadəçi silinsə də audit qeydi qalmalıdır - ona görə FK qurulmur,
        // UserId sadəcə dəyər kimi saxlanılır.
    }
}
