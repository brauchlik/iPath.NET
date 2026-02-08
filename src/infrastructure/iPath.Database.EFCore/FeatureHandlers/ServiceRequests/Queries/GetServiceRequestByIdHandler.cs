
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.ServiceRequests.Queries;


public class GetServiceRequestByIdQueryHandler(iPathDbContext db, IUserSession sess, ILogger<GetServiceRequestByIdQueryHandler> logger)
    : IRequestHandler<GetServiceRequestByIdQuery, Task<ServiceRequestDto>>
{
    public async Task<ServiceRequestDto> Handle(GetServiceRequestByIdQuery request, CancellationToken cancellationToken)
    {
        // Direct projection does not work with Sqlite => better call Entities in one query and project in memory
        var node = await db.ServiceRequests.AsNoTracking()
            .Include(n => n.Owner)
            .Include(n => n.Documents).ThenInclude(a => a.Owner)
            .Include(n => n.Annotations).ThenInclude(a => a.Owner)
            .Include(n => n.UploadFolders)
            .AsSplitQuery()
            .FirstOrDefaultAsync(n => n.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, node);
        Guard.Against.Null(node.GroupId);

        // if not publicly visible, check group
        if (!sess.IsAdmin)
        {
            if (node.Visibility != eNodeVisibility.Public)
            {
                sess.AssertInGroup(node.GroupId);
            }

            var spec = new ServiceRequestIsVisibleSpecifications(sess.IsAuthenticated ? sess.User.Id : null);
            if (!spec.IsSatisfiedBy(node))
            {
                throw new NotAllowedException($"You are not allowed to access case");
            }
        }

        var dto = node.ToDto();
        return dto;
    }
}
