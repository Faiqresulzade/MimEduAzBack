using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Options;
using MimeduAz.Domain.Constants;
using MimeduAz.Domain.Entities;
using MimeduAz.Domain.Enums;

namespace MimeduAz.Application.Services;

public sealed class NotificationService : INotificationService
{
    private readonly IApplicationDbContext _db;
    private readonly IEmailQueue _emails;
    private readonly EmailOptions _options;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IApplicationDbContext db,
        IEmailQueue emails,
        IOptions<EmailOptions> options,
        ILogger<NotificationService> logger)
    {
        _db = db;
        _emails = emails;
        _options = options.Value;
        _logger = logger;
    }

    public async Task NotifyAdminsOfPendingResourceAsync(Resource resource, CancellationToken ct)
    {
        try
        {
            var recipients = await GetAdminEmailsAsync(ct);

            if (recipients.Count == 0)
            {
                _logger.LogWarning(
                    "Moderasiya bildirişi göndərilmədi: admin e-poçtu tapılmadı. ResourceId: {ResourceId}",
                    resource.Id);
                return;
            }

            var authorName = resource.Author?.FullName ?? "Naməlum müəllif";
            var siteUrl = _options.SiteUrl.TrimEnd('/');

            var subject = $"Yeni material moderasiya gözləyir: {resource.Name}";

            var body = $"""
                <div style="font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1F2937">
                  <h2 style="color:#1B2A5B;margin:0 0 16px">Yeni material moderasiya gözləyir</h2>
                  <table cellpadding="6" style="border-collapse:collapse">
                    <tr><td><b>Material</b></td><td>{Escape(resource.Name)}</td></tr>
                    <tr><td><b>Müəllif</b></td><td>{Escape(authorName)}</td></tr>
                    <tr><td><b>Fənn / sinif</b></td><td>{Escape(resource.Subject)} · {resource.Grade}. sinif</td></tr>
                    <tr><td><b>Növ</b></td><td>{DescribeType(resource.Type)}</td></tr>
                    <tr><td><b>Qiymət</b></td><td>{(resource.IsPaid ? $"{resource.Price:0.00} AZN" : "Pulsuz")}</td></tr>
                    {BuildLinkRow(resource)}
                  </table>
                  <p style="margin:20px 0">
                    <a href="{siteUrl}/admin/moderasiya"
                       style="background:#1B2A5B;color:#fff;padding:10px 18px;
                              border-radius:6px;text-decoration:none">
                      Moderasiya növbəsinə keç
                    </a>
                  </p>
                  <p style="color:#6B7280;font-size:12px">
                    Bu məktub MIMEDU.AZ tərəfindən avtomatik göndərilib.
                  </p>
                </div>
                """;

            if (!_emails.TryEnqueue(new EmailMessage(recipients, subject, body)))
            {
                _logger.LogWarning(
                    "Moderasiya bildirişi növbəyə əlavə edilə bilmədi. ResourceId: {ResourceId}", resource.Id);
            }
        }
        catch (Exception ex)
        {
            // Bildiriş resursun yüklənməsini heç vaxt pozmamalıdır.
            _logger.LogError(ex, "Moderasiya bildirişi hazırlanarkən xəta baş verdi.");
        }
    }

    public async Task NotifyAdminsOfPendingExamAsync(Exam exam, CancellationToken ct)
    {
        try
        {
            var recipients = await GetAdminEmailsAsync(ct);

            if (recipients.Count == 0)
            {
                _logger.LogWarning(
                    "Moderasiya bildirişi göndərilmədi: admin e-poçtu tapılmadı. ExamId: {ExamId}",
                    exam.Id);
                return;
            }

            var authorName = exam.Author?.FullName ?? "Naməlum müəllif";
            var siteUrl = _options.SiteUrl.TrimEnd('/');
            var questionCount = exam.Sections.Sum(s => s.Questions.Count);
            var sectionList = string.Join(
                ", ",
                exam.Sections
                    .OrderBy(s => s.OrderIndex)
                    .Select(s => $"{Escape(s.Subject)} ({s.Questions.Count})"));

            var subject = $"Yeni sınaq moderasiya gözləyir: {exam.Name}";

            var body = $"""
                <div style="font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1F2937">
                  <h2 style="color:#1B2A5B;margin:0 0 16px">Yeni sınaq moderasiya gözləyir</h2>
                  <table cellpadding="6" style="border-collapse:collapse">
                    <tr><td><b>Sınaq</b></td><td>{Escape(exam.Name)}</td></tr>
                    <tr><td><b>Müəllif</b></td><td>{Escape(authorName)}</td></tr>
                    <tr><td><b>Fənn / sinif</b></td><td>{Escape(exam.Subject)}{DescribeGrade(exam.Grade)}</td></tr>
                    <tr><td><b>Müddət</b></td><td>{exam.DurationMinutes} dəqiqə</td></tr>
                    <tr><td><b>Keçid balı</b></td><td>{exam.PassPercent}%</td></tr>
                    <tr><td><b>Sual sayı</b></td><td>{questionCount}</td></tr>
                    <tr><td><b>Bölmələr</b></td><td>{sectionList}</td></tr>
                    <tr><td><b>Qiymət</b></td><td>{(exam.IsPaid ? $"{exam.Price:0.00} AZN" : "Pulsuz")}</td></tr>
                  </table>
                  <p style="margin:20px 0">
                    <a href="{siteUrl}/admin/moderasiya"
                       style="background:#1B2A5B;color:#fff;padding:10px 18px;
                              border-radius:6px;text-decoration:none">
                      Moderasiya növbəsinə keç
                    </a>
                  </p>
                  <p style="color:#6B7280;font-size:12px">
                    Bu məktub MIMEDU.AZ tərəfindən avtomatik göndərilib.
                  </p>
                </div>
                """;

            if (!_emails.TryEnqueue(new EmailMessage(recipients, subject, body)))
            {
                _logger.LogWarning(
                    "Sınaq moderasiya bildirişi növbəyə əlavə edilə bilmədi. ExamId: {ExamId}", exam.Id);
            }
        }
        catch (Exception ex)
        {
            // Bildiriş sınağın yaradılmasını heç vaxt pozmamalıdır.
            _logger.LogError(ex, "Sınaq moderasiya bildirişi hazırlanarkən xəta baş verdi.");
        }
    }

    private async Task<List<string>> GetAdminEmailsAsync(CancellationToken ct)
    {
        var adminRoleIds = await _db.Roles
            .AsNoTracking()
            .Where(r => r.Name == AppRoles.Admin)
            .Select(r => r.Id)
            .ToListAsync(ct);

        var adminIds = await _db.UserRoles
            .AsNoTracking()
            .Where(ur => adminRoleIds.Contains(ur.RoleId))
            .Select(ur => ur.UserId)
            .ToListAsync(ct);

        var emails = await _db.Users
            .AsNoTracking()
            .Where(u => adminIds.Contains(u.Id) && u.Email != null)
            .Select(u => u.Email!)
            .ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(_options.ModerationInbox))
        {
            emails.Add(_options.ModerationInbox.Trim());
        }

        return emails
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildLinkRow(Resource resource) =>
        resource.IsLinkBased && !string.IsNullOrWhiteSpace(resource.ExternalUrl)
            ? $"""<tr><td><b>Link</b></td><td><a href="{Escape(resource.ExternalUrl)}">{Escape(resource.ExternalUrl)}</a></td></tr>"""
            : $"""<tr><td><b>Fayl</b></td><td>{Escape(resource.OriginalFileName ?? "—")}</td></tr>""";

    private static string DescribeGrade(int? grade) =>
        grade is > 0 ? $" · {grade}. sinif" : string.Empty;

    private static string DescribeType(ResourceType type) => type switch
    {
        ResourceType.WorkSheet => "İş vərəqi",
        ResourceType.Presentation => "Təqdimat",
        ResourceType.Test => "Test",
        ResourceType.MethodGuide => "Metodik vəsait",
        ResourceType.Video => "Video dərs",
        ResourceType.ExternalLink => "Xarici resurs",
        _ => type.ToString()
    };

    private static string Escape(string value) => WebUtility.HtmlEncode(value);
}
