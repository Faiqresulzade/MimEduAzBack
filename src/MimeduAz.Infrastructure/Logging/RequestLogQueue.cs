using System.Threading.Channels;
using Microsoft.Extensions.Options;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Options;
using MimeduAz.Domain.Entities;

namespace MimeduAz.Infrastructure.Logging;

/// <summary>
/// Audit qeydləri üçün yaddaşdakı məhdud tutumlu növbə.
/// Növbə dolarsa ən köhnə qeyd atılır (<see cref="BoundedChannelFullMode.DropOldest"/>) -
/// log yazmaq üçün heç vaxt sorğu gözlədilmir.
/// </summary>
public sealed class RequestLogQueue : IRequestLogSink
{
    private readonly Channel<RequestLog> _channel;

    public RequestLogQueue(IOptions<RequestLogOptions> options)
    {
        var capacity = Math.Max(1, options.Value.QueueCapacity);

        _channel = Channel.CreateBounded<RequestLog>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ChannelReader<RequestLog> Reader => _channel.Reader;

    public bool TryEnqueue(RequestLog log) => _channel.Writer.TryWrite(log);
}
