using DispatchR.Abstractions.Send;
using iPath.Application.Features.Admin;
using iPath.Domain.Config;
using Microsoft.Extensions.Options;

namespace iPath.API.Services.Cache.Handlers;

public class GetCacheOverviewHandler(
    ICacheManager cm,
    IOptions<CacheSettings> cacheOpts,
    IOptions<iPathConfig> ipathOpts)
    : IRequestHandler<GetCacheOverviewQuery, Task<CacheOverviewDto?>>
{
    public async Task<CacheOverviewDto?> Handle(GetCacheOverviewQuery request, CancellationToken cancellationToken)
    {
        var stats = await cm.GetStatsAsync();
        var drive = new DriveInfo(Path.GetPathRoot(ipathOpts.Value.TempDataPath) ?? ".");
        return new CacheOverviewDto
        {
            TotalSize = stats.TotalSize,
            MaxSize = cacheOpts.Value.MaxCacheSizeBytes,
            EntryCount = stats.EntryCount,
            CheapCount = stats.CheapCount,
            ExpensiveCount = stats.ExpensiveCount,
            FreeDiskBytes = drive.AvailableFreeSpace,
            TempPath = ipathOpts.Value.TempDataPath
        };
    }
}

public class EvictCacheHandler(ICacheManager cm)
    : IRequestHandler<EvictCacheCommand, Task<bool>>
{
    public async Task<bool> Handle(EvictCacheCommand request, CancellationToken cancellationToken)
    {
        await cm.RunNormalEvictionAsync(cancellationToken);
        return true;
    }
}
