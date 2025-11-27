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

        var community = await db.Groups.AsNoTracking()
            .Where(g => g.Id == GroupId)
            .Select(g => new GroupDto(Id: g.Id, Name: g.Name, Visibility: g.Visibility, Owner: g.Owner.ToOwnerDto(), Settings: g.Settings,
                                      Members: g.Members.Select(m => new GroupMemberDto(UserId: m.User.Id, Username: m.User.UserName, Role: m.Role)).ToArray()))
            .FirstOrDefaultAsync(ct);

        Guard.Against.NotFound(GroupId, community);

        return community;
    }

    public async Task<PagedResultList<GroupListDto>> GetGroupListAsync(GetGroupListQuery request, CancellationToken ct = default)
    {
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
            dtoQuery = q.Select(x => new GroupListDto(x.Id, x.Name, x.Visibility));
        }
        else
        {
            var minDate = DateTime.UtcNow.AddYears(-1);
            var uid = sess.User.Id;

            dtoQuery = q.Select(x => new GroupListDto(x.Id, x.Name, x.Visibility,
                x.Nodes.Count(),
                x.Nodes.Count(n => n.CreatedOn > minDate && !n.LastVisits.Any(v => v.UserId == uid)),
                x.Nodes.Count(n => n.Annotations.Any(a => a.CreatedOn > minDate &&
                                                        (!n.LastVisits.Any(v => v.UserId == uid) || a.CreatedOn > n.LastVisits.First(v => v.UserId == uid).Date)))
                ));
        }

        // paginate
        var data = await dtoQuery.ToPagedResultAsync(request, ct);
        return data;
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
            .Include(x => x.Communities)
            .FirstOrDefaultAsync(x => x.Id == request.GroupId, ct);
        Guard.Against.NotFound(request.GroupId.ToString(), group);

        var community = await db.Communities.FindAsync(new object[] { request.CommunityId }, ct);
        Guard.Against.NotFound(request.CommunityId.ToString(), community);


        // prepare changes
        if (request.Remove)
        {
            var toRemove = group.Communities.FirstOrDefault(x => x.CommunityId == request.CommunityId);
            if (toRemove != null)
            {
                group.Communities.Remove(toRemove);
            }
        }
        else
        {
            var exists = group.Communities.Any(x => x.CommunityId == request.CommunityId);
            if (!exists)
            {
                group.Communities.Add(new CommunityGroup()
                {
                    Group = group,
                    Community = community
                });
            }
        }

        var evt = EventEntity.Create<GroupAssignedToCommunityEvent, AssignGroupToCommunityCommand>(request, objectId: group.Id, userId: sess.User.Id);
        await db.EventStore.AddAsync(evt, ct);

        await db.SaveChangesAsync(ct);
        return evt;
    }

    public Task AssignQuestionnaireToGroupAsync(AssignQuestionnaireToGroupCommand cmd, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public async Task<GroupListDto> CreateGroupAsync(CreateGroupCommand cmd, CancellationToken ct = default)
    {
        Guard.Against.NullOrEmpty(cmd.Name);
        await AssertNameNotExists(cmd.Name, null, ct);

        var group = new Group
        {
            Id = Guid.CreateVersion7(),
            Name = cmd.Name,
            Settings = cmd.Settings,
            OwnerId = cmd.OwnerId,
            Visibility = cmd.Visibility ?? eGroupVisibility.MembersOnly,
            CreatedOn = DateTime.UtcNow
        };
        await db.Groups.AddAsync(group, ct);
        await db.SaveChangesAsync(ct);

        if (cmd.CommunityId.HasValue)
        {   
            await AssignGroupToCommunityAsync(new AssignGroupToCommunityCommand(CommunityId: cmd.CommunityId.Value, GroupId: group.Id), ct);
        }

        return group.ToListDto();
    }

    public Task DeleteGroupAsync(DeleteGroupCommand cmd, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }



    public async Task UpdateGroupAsync(UpdateGroupCommand cmd, CancellationToken ct = default)
    {
        var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == cmd.Id, ct);
        Guard.Against.NotFound(cmd.Id, group);


        if (cmd.Name != null)
        {
            await AssertNameNotExists(cmd.Name, group.Id, ct);
            group.Name = cmd.Name;
        }

        if (cmd.Settings != null) group.Settings = cmd.Settings;    
        if (cmd.Visibility.HasValue) group.Visibility = cmd.Visibility.Value;
        if (cmd.OwnerId.HasValue) group.OwnerId = cmd.OwnerId.Value;

        await db.SaveChangesAsync(ct);
    }
    #endregion
}

