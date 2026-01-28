using iPath.Application.Features.CMS;

namespace iPath.EF.Core.FeatureHandlers.CMS.Commands;

public class DeleteWebContentCommandHandler(iPathDbContext db)
    : IRequestHandler<DeleteWebContentCommand, Task>
{
    public Task Handle(DeleteWebContentCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}