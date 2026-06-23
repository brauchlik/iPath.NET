using iPath.Application.Features.Admin;

namespace iPath.Blazor.ServiceLib.Services;

public class NoOpTranslationJobQueue : ITranslationJobQueue
{
    public bool IsPaused { get; set; }
    public bool IsManualJobRunning { get; set; }

    public void EnqueueKey(string key) { }

    public ValueTask<string?> WaitToReadAsync(CancellationToken ct)
    {
        return ValueTask.FromResult<string?>(null);
    }
}
