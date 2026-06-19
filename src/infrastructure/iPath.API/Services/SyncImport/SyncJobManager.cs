using iPath.Application.Features.SyncImport;
using Microsoft.Extensions.DependencyInjection;

namespace iPath.API.Services.SyncImport;

public class SyncJobManager(IServiceScopeFactory scopeFactory, ILogger<SyncJobManager> logger) : ISyncJobManager
{
    private SyncJobState? _current;
    private readonly object _lock = new();

    public SyncJobState? Current { get { lock (_lock) return _current; } }

    public Guid StartSync(int groupId)
        => StartJob(groupId, reimport: false);

    public Guid StartReimport(int groupId)
        => StartJob(groupId, reimport: true);

    private Guid StartJob(int groupId, bool reimport)
    {
        lock (_lock)
        {
            if (_current is { IsRunning: true })
                throw new InvalidOperationException($"Sync job {_current.JobId} for group {_current.GroupId} is already running");

            _current = new SyncJobState { GroupId = groupId };
        }

        var jobId = _current.JobId;
        _ = RunAsync(groupId, jobId, reimport);
        return jobId;
    }

    private async Task RunAsync(int groupId, Guid jobId, bool reimport)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var runner = scope.ServiceProvider.GetRequiredService<ISyncImportRunner>();
            var progress = new Progress<(int Current, int Total, string Status)>(p =>
            {
                lock (_lock)
                {
                    if (_current?.JobId != jobId) return;
                    _current.Current = p.Current;
                    _current.Total = p.Total;
                    _current.Status = p.Status;
                }
            });
            if (reimport)
                await runner.ReimportGroupAsync(groupId, progress);
            else
                await runner.SyncGroupWithProgressAsync(groupId, progress);
            lock (_lock) { if (_current?.JobId == jobId) _current.IsDone = true; }
            logger.LogInformation("Sync job {JobId} for group {GroupId} completed", jobId, groupId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sync job {JobId} for group {GroupId} failed", jobId, groupId);
            lock (_lock) { if (_current?.JobId == jobId) { _current.Error = ex.Message; _current.IsDone = true; } }
        }
    }
}
