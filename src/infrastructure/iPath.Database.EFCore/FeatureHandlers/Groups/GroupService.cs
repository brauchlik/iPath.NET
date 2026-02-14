using Microsoft.Extensions.Logging;
using EFunc = Microsoft.EntityFrameworkCore.EF;

namespace iPath.EF.Core.FeatureHandlers.Groups;

public class GroupService(iPathDbContext db, IUserSession sess, ILogger<GroupService> logger)
    : IGroupService
{
    #region "-- Queries --"
    public async Task<GroupDto> GetGroupByIdAsync(Guid GroupId, CancellationToken ct = default)
    {
        sess.AssertInGroup(GroupId);

        var group = await db.Groups
            .AsNoTracking()
            .Include(g => g.Community).ThenInclude(c => c.Owner)
            .Where(g => g.Id == GroupId)
            .Select(g => new GroupDto(Id: g.Id, Name: g.Name, Visibility: g.Visibility, Owner: g.Owner.ToOwnerDto(),
                                      Community: g.Community.ToListDto(),
                                      Settings: g.Settings,
                                      Members: g.Members.Select(m => new GroupMemberDto(UserId: m.User.Id, Username: m.User.UserName, Role: m.Role, IsConsultant: m.IsConsultant)).ToArray(),
                                      ExtraCommunities: g.ExtraCommunities.Select(c => new CommunityListDto(Id: c.Community.Id, Name: c.Community.Name)).ToArray(),
                                      Questionnaires: g.Quesionnaires.Select(q => new QuestionnaireForGroupDto(qId: q.QuestionnaireId,
                                      QuestinnaireId: q.Questionnaire.QuestionnaireId,
                                      QuestinnaireName: q.Questionnaire.Name,
                                      Usage: q.Usage,
                                      Settings: q.Questionnaire.Settings,
                                      Priority: q.Priority,
                                      ExplicitVersion: q.ExplicitVersion)).ToArray()))
            .FirstOrDefaultAsync(ct);

        Guard.Against.NotFound(GroupId, group);

        logger.LogInformation("Group '{group}' loaded by User '{user}'", group.Name, sess.Username);
        return group;
    }

    public async Task<PagedResultList<GroupListDto>> GetGroupListAsync(GetGroupListQuery request, CancellationToken ct = default)
    {
        var q = db.Groups.AsNoTracking();

        if (request.AdminList)
        {
            // only admins can get the full list
            sess.AssertInRole("Admin");
        }
        else if (request.CommunityId.HasValue)
        {
            q = q.Where(g => g.CommunityId == request.CommunityId.Value || g.ExtraCommunities.Any(cg => cg.CommunityId == request.CommunityId.Value));
        }
        else if (sess.User is not null)
        {
            // users group list
            q = q.Where(g => g.Members.Any(m => m.UserId == sess.User.Id));
        }
        else
        {
            throw new NotAllowedException();
        }

        // filter
        q = q.ApplyQuery(request, "Name ASC");

        // project
        IQueryable<GroupListDto> dtoQuery;
        if (!request.IncludeCounts)
        {
            dtoQuery = q.Select(x => new GroupListDto(x.Id, x.Name, x.Visibility));
        }
        else
        {
            var minDate = DateTime.UtcNow.AddYears(-1);
            var uid = sess.User.Id;

            dtoQuery = q.Select(x => new GroupListDto(x.Id, x.Name, x.Visibility,
                x.ServiceRequests.Count(),
                // do not count drafts
                x.ServiceRequests.Count(n => !n.IsDraft && n.CreatedOn > minDate && !n.LastVisits.Any(v => v.UserId == uid)),
                // do not count drafts and also don't count users own comments
                x.ServiceRequests.Count(n => !n.IsDraft && n.Annotations.Any(a => a.CreatedOn > minDate && a.OwnerId != uid &&
                                                        (!n.LastVisits.Any(v => v.UserId == uid) || a.CreatedOn > n.LastVisits.First(v => v.UserId == uid).Date)))
                ));
        }

        // paginate
        var data = await dtoQuery.ToPagedResultAsync(request, ct);
        return data;
    }



    public async Task<PagedResultList<GroupMemberDto>> GetGroupMembersAsync(GetGroupMembersQuery query, CancellationToken ct = default)
    {
        if (!sess.IsAdmin && !sess.IsGroupModerator(query.GroupId))
            throw new NotAllowedException();

        var q = db.Set<GroupMember>().AsNoTracking()
            .Include(m => m.User)
            .Where(m => m.GroupId == query.GroupId)
            .ApplyQuery(query);


        if (!string.IsNullOrEmpty(query.SearchString))
        {
            var s = $"%{query.SearchString}%";
            q = q.Where(m => EFunc.Functions.Like(m.User.UserName, s) || EFunc.Functions.Like(m.User.Email, s));
        }

        var projected = q.Select(m => new GroupMemberDto(UserId: m.User.Id, Username: m.User.UserName, Role: m.Role, IsConsultant: m.IsConsultant));
        return await projected.ToPagedResultAsync(query, ct);
    }


    #endregion


    #region "-- Commands --"
    private async Task AssertNameNotExists(string name, Guid? id, CancellationToken ct)
    {
        var q = db.Groups
            .Where(g => EFunc.Functions.Like(g.Name, name));

        if (id.HasValue)
            q = q.Where(g => g.Id != id.Value);

        if (await q.AnyAsync(ct))
            throw new ArgumentException("Group name exists already");
    }


    public async Task<GroupAssignedToCommunityEvent> AssignGroupToCommunityAsync(AssignGroupToCommunityCommand request, CancellationToken ct = default)
    {
        Guard.Against.Null(sess.User);
        sess.AssertInRole("Admin");

        var group = await db.Groups
            .Include(x => x.ExtraCommunities)
            .FirstOrDefaultAsync(x => x.Id == request.GroupId, ct);
        Guard.Against.NotFound(request.GroupId.ToString(), group);

        var community = await db.Communities.FindAsync(new object[] { request.CommunityId }, ct);
        Guard.Against.NotFound(request.CommunityId.ToString(), community);


        // prepare changes
        if (request.Remove)
        {
            var toRemove = group.ExtraCommunities.FirstOrDefault(x => x.CommunityId == request.CommunityId);
            if (toRemove != null)
            {
                group.ExtraCommunities.Remove(toRemove);
            }
        }
        else
        {
            var exists = group.ExtraCommunities.Any(x => x.CommunityId == request.CommunityId);
            if (!exists)
            {
                group.AssignToCommunity(community);
            }
        }

        var evt = EventEntity.Create<GroupAssignedToCommunityEvent, AssignGroupToCommunityCommand>(request, objectId: group.Id, userId: sess.User.Id);
        await db.EventStore.AddAsync(evt, ct);

        await db.SaveChangesAsync(ct);
        return evt;
    }


    public async Task<GroupListDto> CreateGroupAsync(CreateGroupCommand cmd, CancellationToken ct = default)
    {
        Guard.Against.NullOrEmpty(cmd.Name);
        await AssertNameNotExists(cmd.Name, null, ct);

        var owner = await db.Users.FindAsync(cmd.OwnerId, ct);
        Guard.Against.NotFound(cmd.OwnerId, owner);

        var community = await db.Communities.FindAsync(cmd.CommunityId, ct);
        Guard.Against.NotFound(cmd.CommunityId, community);

        var group = Group.Create(Name: cmd.Name, Owner: owner, community);
        group.Settings = cmd.Settings;
        group.Visibility = cmd.Visibility ?? eGroupVisibility.MembersOnly;

        await db.Groups.AddAsync(group, ct);
        await db.SaveChangesAsync(ct);

        return group.ToListDto();
    }

    public async Task DeleteGroupAsync(DeleteGroupCommand cmd, CancellationToken ct = default)
    {
        var group = await db.Groups.FindAsync(cmd.Id, ct);
        Guard.Against.NotFound(cmd.Id, group);

        // simply set inactive
        group.Visibility = eGroupVisibility.Inactive;

        await db.SaveChangesAsync(ct);
    }



    public async Task UpdateGroupAsync(UpdateGroupCommand cmd, CancellationToken ct = default)
    {
        var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == cmd.Id, ct);
        Guard.Against.NotFound(cmd.Id, group);


        if (cmd.Name != null)
        {
            await AssertNameNotExists(cmd.Name, group.Id, ct);
            group.RenameGroup(cmd.Name);
        }

        if (cmd.Settings != null) group.Settings = cmd.Settings;
        if (cmd.Visibility.HasValue) group.Visibility = cmd.Visibility.Value;
        if (cmd.OwnerId.HasValue) group.OwnerId = cmd.OwnerId.Value;
        if (cmd.CommunityId.HasValue) group.CommunityId = cmd.CommunityId.Value;

        await db.SaveChangesAsync(ct);
    }


    public async Task DeleteGroupDraftsAsync(Guid groupId, CancellationToken ct = default)
    {
        if (sess.IsAdmin)
        {
            var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == groupId, ct);
            Guard.Against.NotFound(groupId, group);

            await db.ServiceRequests.Where(x => x.IsDraft && x.GroupId == group.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(b => b.DeletedOn, DateTime.UtcNow), ct);
        }
    }
    #endregion
}

