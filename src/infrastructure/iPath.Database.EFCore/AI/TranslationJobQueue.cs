using System.Threading.Channels;
using iPath.Application.Features.Admin;

namespace iPath.Database.EFCore.AI;

public class TranslationJobQueue : ITranslationJobQueue
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    public bool IsPaused { get; set; }
    public bool IsManualJobRunning { get; set; }

    public void EnqueueKey(string key)
    {
        _channel.Writer.TryWrite(key);
    }

    /// <summary>
    /// Wait for a single key with cancellation. Returns null on cancellation/timeout.
    /// </summary>
    public async ValueTask<string?> WaitToReadAsync(CancellationToken ct)
    {
        try
        {
            while (await _channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                if (_channel.Reader.TryRead(out var key))
                    return key;
            }
        }
        catch (OperationCanceledException)
        {
        }
        return null;
    }
}
