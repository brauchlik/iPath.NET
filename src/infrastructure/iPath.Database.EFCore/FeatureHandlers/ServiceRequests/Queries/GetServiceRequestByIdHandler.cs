using iPath.Application.Contracts;
using iPath.Application.Features.Annotations;
using iPath.Application.Features.Documents;

namespace iPath.EF.Core.FeatureHandlers.ServiceRequests.Queries;


public class GetServiceRequestByIdQueryHandler(
    iPathDbContext db,
    IUserSession sess,
    IThumbnailQueue thumbnailQueue,
    IEnumerable<IConversionPlugin> conversionPlugins,
    ILogger<GetServiceRequestByIdQueryHandler> logger)
    : IRequestHandler<GetServiceRequestByIdQuery, Task<ServiceRequestDto>>
{
    public async Task<ServiceRequestDto> Handle(GetServiceRequestByIdQuery request, CancellationToken cancellationToken)
    {
        // Direct projection does not work with Sqlite => better call Entities in one query and project in memory
        var node = await db.ServiceRequests.AsNoTracking()
            .Include(n => n.Owner)
            .Include(n => n.Documents.Where(a => request.inclDeletedData || !a.DeletedOn.HasValue))
                .ThenInclude(a => a.Owner)
            .Include(n => n.Annotations.Where(d => request.inclDeletedData || !d.DeletedOn.HasValue))
                .ThenInclude(a => a.Owner)
            .Include(n => n.UploadFolders)
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

        // Enqueue thumbnail jobs for documents missing thumbnails
        if (node.Documents is not null)
        {
            foreach (var doc in node.Documents)
            {
                var ext = Path.GetExtension(doc.File?.Filename ?? "");
                if (string.IsNullOrEmpty(ext)) continue;

                if (!string.IsNullOrEmpty(doc.File?.ThumbData)) continue;
                if ((doc.File?.ThumbRetryCount ?? 0) >= 3) continue;

                if (conversionPlugins.Any(p => p.CanHandle(ext)))
                {
                    await thumbnailQueue.EnqueueAsync(doc.Id, cancellationToken);
                }
            }
        }

        return dto;
    }
}
