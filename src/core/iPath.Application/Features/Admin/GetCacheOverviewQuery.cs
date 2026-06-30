namespace iPath.Application.Features.Admin;

public record GetCacheOverviewQuery() : IRequest<GetCacheOverviewQuery, Task<CacheOverviewDto?>>;
public record EvictCacheCommand() : IRequest<EvictCacheCommand, Task<bool>>;
