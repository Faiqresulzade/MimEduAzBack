using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Mappings;
using MimeduAz.Application.Common.Options;
using MimeduAz.Contracts.Common;
using MimeduAz.Contracts.Resources;
using MimeduAz.Domain.Entities;
using MimeduAz.Domain.Enums;

namespace MimeduAz.Application.Services;

public sealed class ResourceService : IResourceService
{
    private readonly IApplicationDbContext _db;
    private readonly IFileStorageService _files;
    private readonly ICurrentUserService _currentUser;
    private readonly FileStorageOptions _storage;
    private readonly ILogger<ResourceService> _logger;

    public ResourceService(
        IApplicationDbContext db,
        IFileStorageService files,
        ICurrentUserService currentUser,
        IOptions<FileStorageOptions> storage,
        ILogger<ResourceService> logger)
    {
        _db = db;
        _files = files;
        _currentUser = currentUser;
        _storage = storage.Value;
        _logger = logger;
    }

    public async Task<PagedResult<ResourceDto>> GetAsync(ResourceQuery query, CancellationToken ct)
    {
        // Status filtri yalnız adminə açıqdır; digərləri həmişə yalnız təsdiqlənmişləri görür.
        var status = _currentUser.IsAdmin ? query.Status ?? ResourceStatus.Approved : ResourceStatus.Approved;

        var q = _db.Resources
            .AsNoTracking()
            .Include(r => r.Author)
            .Include(r => r.Quiz)
            .Where(r => r.Status == status);

        if (!string.IsNullOrWhiteSpace(query.Subject))
        {
            var subject = query.Subject.Trim();
            q = q.Where(r => r.Subject == subject);
        }

        if (query.Grade is > 0)
        {
            q = q.Where(r => r.Grade == query.Grade);
        }

        if (query.Type is not null)
        {
            q = q.Where(r => r.Type == query.Type);
        }

        if (query.IsPaid is not null)
        {
            q = q.Where(r => r.IsPaid == query.IsPaid);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Provayderdən asılı olmasın deyə ToLower/Contains istifadə olunur.
            var term = query.Search.Trim().ToLower();
            q = q.Where(r => r.Name.ToLower().Contains(term));
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<ResourceDto>
        {
            Items = items.Select(r => r.ToDto()).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<ResourceDetailDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var resource = await _db.Resources
            .AsNoTracking()
            .Include(r => r.Author)
            .Include(r => r.Quiz)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw NotFoundException.For("Resurs", id);

        // Təsdiqlənməmiş resursu yalnız müəllifi və admin görə bilər.
        if (resource.Status != ResourceStatus.Approved
            && !_currentUser.IsAdmin
            && resource.AuthorId != _currentUser.UserId)
        {
            throw NotFoundException.For("Resurs", id);
        }

        return resource.ToDetailDto();
    }

    public async Task<ResourceDetailDto> CreateAsync(
        CreateResourceRequest request,
        ResourceFileUpload file,
        CancellationToken ct)
    {
        var authorId = _currentUser.RequireUserId();

        ValidateFile(file);

        var storedPath = await _files.SaveAsync(file.Content, file.FileName, ct);

        var resource = new Resource
        {
            Name = request.Name.Trim(),
            Subject = request.Subject.Trim(),
            Grade = request.Grade,
            Type = request.Type,
            AuthorId = authorId,
            IsPaid = request.IsPaid,
            // Pulsuz resursda qiymət həmişə sıfırdır, göndərilən dəyərdən asılı olmayaraq.
            Price = request.IsPaid ? request.Price : 0m,
            Status = ResourceStatus.Pending,
            FilePath = storedPath,
            OriginalFileName = Path.GetFileName(file.FileName),
            CreatedAt = DateTime.UtcNow
        };

        _db.Resources.Add(resource);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Yeni resurs yükləndi və moderasiyaya göndərildi. ResourceId: {ResourceId}", resource.Id);

        resource.Author = await _db.Users.FirstOrDefaultAsync(u => u.Id == authorId, ct);
        return resource.ToDetailDto();
    }

    public async Task<ResourceDetailDto> CreateLinkAsync(CreateResourceLinkRequest request, CancellationToken ct)
    {
        var authorId = _currentUser.RequireUserId();

        if (request.Type is not (ResourceType.Video or ResourceType.ExternalLink))
        {
            throw new ValidationFailedException(
                "type", "Bu endpoint yalnız Video və ExternalLink tipləri üçündür; fayl əsaslı resurslar multipart ilə göndərilir.");
        }

        // Xarici linki ödənişli satmaq mənasızdır - link bir dəfə paylaşıldıqdan
        // sonra ona nəzarət etmək mümkün deyil.
        if (request.Type == ResourceType.ExternalLink && request.IsPaid)
        {
            throw new ValidationFailedException(
                "isPaid", "Xarici link resursu yalnız pulsuz ola bilər. Ödənişli satış üçün video dərs və ya fayl yükləyin.");
        }

        var resource = new Resource
        {
            Name = request.Name.Trim(),
            Subject = request.Subject.Trim(),
            Grade = request.Grade,
            Type = request.Type,
            AuthorId = authorId,
            IsPaid = request.IsPaid,
            Price = request.IsPaid ? request.Price : 0m,
            Status = ResourceStatus.Pending,
            ExternalUrl = request.ExternalUrl.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.Resources.Add(resource);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Link əsaslı resurs yükləndi və moderasiyaya göndərildi. ResourceId: {ResourceId}, Tip: {Type}",
            resource.Id, resource.Type);

        resource.Author = await _db.Users.FirstOrDefaultAsync(u => u.Id == authorId, ct);
        return resource.ToDetailDto();
    }

    public async Task<ResourceDownloadDto> DownloadAsync(Guid id, CancellationToken ct)
    {
        var resource = await _db.Resources
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw NotFoundException.For("Resurs", id);

        var isOwner = _currentUser.UserId is not null && resource.AuthorId == _currentUser.UserId;

        if (resource.Status != ResourceStatus.Approved && !isOwner && !_currentUser.IsAdmin)
        {
            throw NotFoundException.For("Resurs", id);
        }

        if (resource.IsPaid && !isOwner && !_currentUser.IsAdmin)
        {
            var userId = _currentUser.UserId
                ?? throw new UnauthorizedException("Ödənişli resursu endirmək üçün daxil olun.");

            var purchased = await _db.OrderItems
                .AnyAsync(oi =>
                    oi.ItemType == CatalogItemType.Resource &&
                    oi.ItemId == resource.Id &&
                    oi.Order!.UserId == userId &&
                    oi.Order.Status == OrderStatus.Paid, ct);

            if (!purchased)
            {
                throw new ForbiddenException("Bu resursu endirmək üçün əvvəlcə satın almalısınız.");
            }
        }

        // Link əsaslı resursda fayl yoxdur - xarici ünvan qaytarılır.
        if (resource.IsLinkBased)
        {
            if (string.IsNullOrWhiteSpace(resource.ExternalUrl))
            {
                throw new NotFoundException("Bu resursun linki mövcud deyil.");
            }

            resource.Downloads += 1;
            await _db.SaveChangesAsync(ct);

            return new ResourceDownloadDto(
                resource.Id,
                resource.Name,
                resource.ExternalUrl,
                IsExternal: true,
                resource.Downloads);
        }

        if (string.IsNullOrWhiteSpace(resource.FilePath))
        {
            throw new NotFoundException("Bu resursun faylı mövcud deyil.");
        }

        resource.Downloads += 1;
        await _db.SaveChangesAsync(ct);

        return new ResourceDownloadDto(
            resource.Id,
            resource.OriginalFileName ?? Path.GetFileName(resource.FilePath),
            _files.GetPublicUrl(resource.FilePath),
            IsExternal: false,
            resource.Downloads);
    }

    public async Task<IReadOnlyList<ResourceDto>> GetMineAsync(CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var resources = await _db.Resources
            .AsNoTracking()
            .Include(r => r.Author)
            .Include(r => r.Quiz)
            .Where(r => r.AuthorId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return resources.Select(r => r.ToDto()).ToList();
    }

    public async Task<AuthorProfileDto> GetAuthorProfileAsync(Guid authorId, CancellationToken ct)
    {
        var author = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == authorId, ct)
            ?? throw NotFoundException.For("Müəllif", authorId);

        // İctimai profildə yalnız təsdiqlənmiş resurslar görünür.
        var resources = await _db.Resources
            .AsNoTracking()
            .Include(r => r.Author)
            .Include(r => r.Quiz)
            .Where(r => r.AuthorId == authorId && r.Status == ResourceStatus.Approved)
            .OrderByDescending(r => r.Downloads)
            .ToListAsync(ct);

        return new AuthorProfileDto(
            author.Id,
            author.FullName,
            author.Subject,
            resources.Count,
            resources.Sum(r => r.Downloads),
            resources.Select(r => r.ToDto()).ToList());
    }

    private void ValidateFile(ResourceFileUpload file)
    {
        if (file.Length <= 0)
        {
            throw new ValidationFailedException("file", "Fayl boşdur.");
        }

        if (file.Length > _storage.MaxFileSizeBytes)
        {
            var limitMb = _storage.MaxFileSizeBytes / (1024 * 1024);
            throw new ValidationFailedException("file", $"Faylın ölçüsü {limitMb} MB-dan çox ola bilməz.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension)
            || !_storage.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            var allowed = string.Join(", ", _storage.AllowedExtensions);
            throw new ValidationFailedException("file", $"Yalnız bu formatlar qəbul olunur: {allowed}.");
        }
    }
}
