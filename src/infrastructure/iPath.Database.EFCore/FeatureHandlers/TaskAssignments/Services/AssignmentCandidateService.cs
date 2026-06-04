using iPath.Application.Features.TaskAssignments;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.TaskAssignments.Services;

public class AssignmentCandidateService(
    iPathDbContext db,
    ILogger<AssignmentCandidateService> logger)
    : IAssignmentCandidateService
{
    public async Task<Guid?> FindBestCandidateAsync(Guid groupId, Guid serviceRequestId, CancellationToken ct = default)
    {
        var candidates = await GetCandidateOrderAsync(groupId, serviceRequestId, ct);
        return candidates.FirstOrDefault();
    }

    public async Task<List<Guid>> GetCandidateOrderAsync(Guid groupId, Guid serviceRequestId, CancellationToken ct = default)
    {
        var sr = await db.ServiceRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == serviceRequestId, ct);

        if (sr?.Description?.BodySite is null)
        {
            return await db.Groups
                .AsNoTracking()
                .Where(g => g.Id == groupId)
                .SelectMany(g => g.Members
                    .Where(m => m.IsConsultant && m.Role >= eMemberRole.User)
                    .Select(m => m.UserId))
                .ToListAsync(ct);
        }

        var consultants = await db.Groups
            .AsNoTracking()
            .Where(g => g.Id == groupId)
            .SelectMany(g => g.Members
                .Where(m => m.IsConsultant && m.Role >= eMemberRole.User)
                .Select(m => new
                {
                    m.UserId,
                    m.NotificationSettings!.BodySiteFilter,
                    m.NotificationSettings!.UseProfileBodySiteFilter,
                    ProfileBodySite = m.User.Profile.SpecialisationBodySite
                }))
            .ToListAsync(ct);

        var bodySite = sr.Description.BodySite;
        var matched = new List<Guid>();
        var unmatched = new List<Guid>();

        foreach (var c in consultants)
        {
            var filter = c.UseProfileBodySiteFilter ? c.ProfileBodySite : c.BodySiteFilter;
            if (filter is not null)
            {
                matched.Add(c.UserId);
            }
            else
            {
                unmatched.Add(c.UserId);
            }
        }

        return matched.Concat(unmatched).ToList();
    }
}
