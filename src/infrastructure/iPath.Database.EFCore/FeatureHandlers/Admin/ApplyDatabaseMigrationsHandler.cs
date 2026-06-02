using iPath.Application.Features.Admin;

namespace iPath.EF.Core.FeatureHandlers.Admin;

public class ApplyDatabaseMigrationsHandler(iPathDbContext db, IMediator mediator, ILogger<ApplyDatabaseMigrationsHandler> logger)
    : IRequestHandler<ApplyDatabaseMigrationsCommand, Task<DatabaseStatusDto>>
{
    public async Task<DatabaseStatusDto> Handle(ApplyDatabaseMigrationsCommand request, CancellationToken ct)
    {
        try
        {
            await db.Database.MigrateAsync(ct);
            logger.LogInformation("Database migrations applied successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error applying database migrations");
            throw;
        }

        return await mediator.Send(new GetDatabaseStatusQuery(), ct);
    }
}
