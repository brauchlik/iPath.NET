namespace iPath.Application.Features.Admin;

public interface ITranslationJobQueue
{
    void EnqueueKey(string key);
    ValueTask<string?> WaitToReadAsync(CancellationToken ct);
    bool IsPaused { get; set; }
    bool IsManualJobRunning { get; set; }
}
