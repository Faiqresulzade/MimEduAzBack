using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Common.Options;

namespace MimeduAz.Application.Services;

/// <summary>Müəllim başına şəkil oxuma limiti (dəqiqəlik + sutkalıq).</summary>
public interface IKsqScanRateLimiter
{
    /// <summary>Limit dolubsa <see cref="TooManyRequestsException"/> atır, əks halda sorğunu sayır.</summary>
    void Acquire(Guid userId);
}

/// <summary>
/// Yaddaşda işləyən sürüşən pəncərə. Tətbiq bir instansda işlədiyi üçün paylaşılan
/// keş lazım deyil; restart-da sayğaclar sıfırlanır — bu, limit üçün qəbul ediləndir.
/// </summary>
public sealed class KsqScanRateLimiter : IKsqScanRateLimiter
{
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Day = TimeSpan.FromDays(1);

    private readonly ConcurrentDictionary<Guid, Queue<DateTimeOffset>> _hits = new();
    private readonly KsqOptions _options;
    private readonly TimeProvider _time;

    public KsqScanRateLimiter(IOptions<KsqOptions> options, TimeProvider time)
    {
        _options = options.Value;
        _time = time;
    }

    public void Acquire(Guid userId)
    {
        var now = _time.GetUtcNow();
        var queue = _hits.GetOrAdd(userId, _ => new Queue<DateTimeOffset>());

        lock (queue)
        {
            while (queue.Count > 0 && now - queue.Peek() >= Day)
            {
                queue.Dequeue();
            }

            if (queue.Count >= _options.ScansPerDay)
            {
                throw new TooManyRequestsException(
                    $"Sutkalıq limit ({_options.ScansPerDay} şəkil) dolub. Sabah yenidən cəhd edin.");
            }

            var lastMinute = queue.Count(hit => now - hit < Minute);
            if (lastMinute >= _options.ScansPerMinute)
            {
                throw new TooManyRequestsException(
                    $"Dəqiqədə ən çox {_options.ScansPerMinute} şəkil oxumaq olar. Bir az gözləyib yenidən cəhd edin.");
            }

            queue.Enqueue(now);
        }
    }
}
