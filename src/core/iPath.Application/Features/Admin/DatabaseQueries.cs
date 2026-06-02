namespace iPath.Application.Features.Admin;

public record GetDatabaseStatusQuery()
    : IRequest<GetDatabaseStatusQuery, Task<DatabaseStatusDto>>;

public record ApplyDatabaseMigrationsCommand()
    : IRequest<ApplyDatabaseMigrationsCommand, Task<DatabaseStatusDto>>;

public record GetDatabaseTableCountsQuery()
    : IRequest<GetDatabaseTableCountsQuery, Task<List<TableRowCountDto>>>;
