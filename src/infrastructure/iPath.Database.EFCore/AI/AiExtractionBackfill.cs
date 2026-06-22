using iPath.Application.AI;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace iPath.Database.EFCore.AI;

public class AiExtractionBackfill : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AiExtractionBackfill> _logger;

    public AiExtractionBackfill(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<AiExtractionBackfill> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var backfillEnabled = _configuration.GetValue<bool>("AiSettings:BackfillEnabled");
        if (!backfillEnabled)
        {
            _logger.LogInformation("AiExtractionBackfill is disabled via configuration.");
            return;
        }

        _logger.LogInformation("AiExtractionBackfill starting - searching for unprocessed cases");

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<iPathDbContext>();
        var queue = scope.ServiceProvider.GetRequiredService<IAiExtractionQueue>();

        var pendingIds = await db.ServiceRequests
            .Where(c => !db.CaseIngestionLineages.Any(l => l.CaseId == c.Id))
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        if (pendingIds.Count == 0)
        {
            _logger.LogInformation("AiExtractionBackfill - no unprocessed cases found");
            return;
        }

        foreach (var id in pendingIds)
        {
            await queue.EnqueueAsync(id);
        }

        _logger.LogInformation("AiExtractionBackfill - enqueued {Count} unprocessed cases", pendingIds.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
