namespace iPath.Application.AI;

public interface IAiTranscriptionQueue
{
    ValueTask EnqueueAsync(Guid caseId);
    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct);
    bool IsInQueue(Guid caseId);
    int GetQueueCount();
}
