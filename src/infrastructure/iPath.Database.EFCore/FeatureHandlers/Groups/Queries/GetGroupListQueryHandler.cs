using iPath.Application.Exceptions;
using System.Linq.Dynamic.Core;

namespace iPath.EF.Core.FeatureHandlers.Groups.Queries;

public class GerCommunityListQueryHandler (iPathDbContext db, IUserSession sess)
    : IRequestHandler<GetGroupListQuery, Task<PagedResultList<GroupListDto>>>
{
    public async Task<PagedResultList<GroupListDto>> Handle(GetGroupListQuery request, CancellationToken cancellationToken)
    {
        // prepare query
        var q = db.Groups.AsNoTracking();

        if (request.AdminList)
        {
            // only admins can get the full list
            sess.AssertInRole("Admin");
        }
        else if (sess.User is not null)
        {
            // users group list
            q = q.Where(g => sess.GroupIds().Contains(g.Id));
        }
        else
        {
            throw new NotAllowedException();
        }

        // filter
        q = q.ApplyQuery(request);

        // project
        IQueryable<GroupListDto> dtoQuery;
        if (!request.IncludeCounts)
        {
            dtoQuery = q.Select(x => new GroupListDto(x.Id, x.Name));
        }
        else
        {
            var minDate = DateTime.UtcNow.AddYears(-1);
            var uid = sess.User.Id;

            dtoQuery = q.Select(x => new GroupListDto(x.Id, x.Name, 
                x.Nodes.Count(), 
                x.Nodes.Count(n => n.CreatedOn > minDate && !n.LastVisits.Any(v => v.UserId == uid)),
                x.Nodes.Count(n => n.Annotations.Any(a => a.CreatedOn > minDate &&
                                                        (!n.LastVisits.Any(v => v.UserId == uid) || a.CreatedOn > n.LastVisits.First(v => v.UserId == uid).Date)))
                ));
        }

        // paginate
        var data = await dtoQuery.ToPagedResultAsync(request, cancellationToken);
        return data;
    }
}
