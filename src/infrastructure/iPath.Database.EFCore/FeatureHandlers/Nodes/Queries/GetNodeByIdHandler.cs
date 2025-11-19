
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.Nodes.Queries;


public class GetRootNodeByIdQueryHandler(iPathDbContext db, IUserSession sess, ILogger<GetRootNodeByIdQueryHandler> logger)
    : IRequestHandler<GetRootNodeByIdQuery, Task<NodeDto>>
{
    public async Task<NodeDto> Handle(GetRootNodeByIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Direct projection does not work with Sqlite => better call Entities in one query and project in memory
            var node = await db.Nodes.AsNoTracking()
                .Include(n => n.Owner)
                .Include(n => n.ChildNodes)
                .Include(n => n.Annotations)
                .FirstOrDefaultAsync(n => n.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, node);
            Guard.Against.Null(node.GroupId);
            sess.AssertInGroup(node.GroupId.Value);

            var dto = node.ToDto();

            return dto;

        }
        catch (Exception ex)
        {
            logger.LogError($"Error while loading node {request.Id}", ex);
        }
        return null;
    }
}


/*
            var node = await db.Nodes.AsNoTracking()
                .Select(n => new NodeDto
                {
                    Id = n.Id,
                    NodeType = n.NodeType,
                    GroupId = n.GroupId,
                    OwnerId = n.OwnerId,
                    Owner = new OwnerDto(n.OwnerId, n.Owner.UserName),
                    Description = n.Description,
                    File = n.File,
                    Annotations = n.Annotations.Select(a => new AnnotationDto
                    {
                        Id = a.Id,
                        CreatedOn = a.CreatedOn,
                        OwnerId = a.OwnerId,
                        Owner = new OwnerDto(n.OwnerId, n.Owner.UserName),
                        Text = a.Text
                    }).ToArray(),
                    ChildNodes = n.ChildNodes.Select(n => new NodeDto
                    {
                        Id = n.Id,
                        NodeType = n.NodeType,
                        GroupId = n.GroupId,
                        ParentNodeId = n.ParentNodeId,
                        RootNodeId = n.RootNodeId,
                        OwnerId = n.OwnerId,
                        Owner = new OwnerDto(n.OwnerId, n.Owner.UserName),
                        Description = n.Description,
                        File = n.File,
                    }).ToArray()
                })
                .FirstOrDefaultAsync(n => n.GroupId.HasValue && n.Id == request.Id);
            */