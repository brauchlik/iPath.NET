namespace iPath.Application.Features.Admin;

public record GetCacheOverviewQuery() : IRequest<GetCacheOverviewQuery, Task<CacheOverviewDto?>>;
public record EvictCacheCommand() : IRequest<EvictCacheCommand, Task<bool>>;
public record SyncCacheCommand() : IRequest<SyncCacheCommand, Task<CacheSyncResult>>;
