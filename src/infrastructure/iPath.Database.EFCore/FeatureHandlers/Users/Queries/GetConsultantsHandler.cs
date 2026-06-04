using iPath.Application.Coding;
using iPath.Application.Features.Users;
using iPath.Application.Querying;
using iPath.EF.Core.Database;
using Microsoft.Extensions.DependencyInjection;

namespace iPath.EF.Core.FeatureHandlers.Users.Queries;

public class GetConsultantsHandler(iPathDbContext db, IServiceProvider sp)
    : IRequestHandler<GetConsultantsQuery, Task<PagedResultList<ConsultantDto>>>
{
    public async Task<PagedResultList<ConsultantDto>> Handle(GetConsultantsQuery request, CancellationToken cancellationToken)
    {
        var q = db.Set<GroupMember>().AsNoTracking()
            .Where(gm => gm.IsConsultant)
            .AsQueryable();

        if (request.GroupId.HasValue)
            q = q.Where(gm => gm.GroupId == request.GroupId.Value);

        if (request.CommunityId.HasValue)
            q = q.Where(gm => gm.Group.CommunityId == request.CommunityId.Value);

        q = q.ApplyQuery(request, "User.UserName ASC");

        if (!string.IsNullOrEmpty(request.SearchString))
        {
            q = q.Where(gm => Microsoft.EntityFrameworkCore.EF.Functions.Like(gm.User.UserName, $"%{request.SearchString}%")
                || Microsoft.EntityFrameworkCore.EF.Functions.Like(gm.User.Email, $"%{request.SearchString}%"));
        }

        var dto = await q.Select(gm => new ConsultantDto(
            Id: gm.UserId,
            Username: gm.User.UserName,
            Email: gm.User.Email,
            Initials: gm.User.Profile.Initials,
            Specialisation: gm.User.Profile.Specialisation,
            BodySiteFilter: gm.User.Profile.SpecialisationBodySite,
            Roles: gm.User.Roles.Select(r => r.Name).ToArray()
        )).ToListAsync(cancellationToken);

        if (!string.IsNullOrEmpty(request.BodySiteCode))
        {
            var coding = sp.GetRequiredKeyedService<CodingService>("icdo");
            dto = dto.Where(c => c.BodySiteFilter is null ||
                coding.InConceptFilter(request.BodySiteCode, c.BodySiteFilter)).ToList();
        }

        var total = dto.Count;
        var pageSize = request.PageSize ?? 10;
        var page = dto.Skip(request.Page * pageSize).Take(pageSize).ToList();

        return new PagedResultList<ConsultantDto>(total, page);
    }
}
