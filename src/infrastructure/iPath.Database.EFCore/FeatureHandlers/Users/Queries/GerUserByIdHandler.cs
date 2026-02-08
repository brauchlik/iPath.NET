namespace iPath.EF.Core.FeatureHandlers.Users.Queries;

public class GetUserByIdHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<GetUserByIdQuery, Task<UserDto>>
{
    public async Task<UserDto> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking()
            .Select(u => new UserDto
            {
                Id = u.Id,
                CreatedOn = u.CreatedOn,
                Username = u.UserName,
                Email = u.Email,
                Profile = u.Profile,
                IsActive = u.IsActive,
                IsNew = u.IsNew,
                Roles = u.Roles.Select(r => new RoleDto(r.Id, r.Name)).ToArray(),
                UploadFolderId = u.UploadFolders.FirstOrDefault().Id,
                GroupMembership = u.GroupMembership.Select(m => new UserGroupMemberDto(GroupId: m.Group.Id, Groupname: m.Group.Name, 
                    Role: m.Role, IsConsultant: m.IsConsultant)).ToArray(),
                CommunityMembership = u.CommunityMembership.Select(m => new CommunityMemberDto(CommunityId: m.Community.Id, UserId: m.UserId, 
                    Role: m.Role, IsConsultant: m.IsConsultant, Communityname: m.Community.Name)).ToArray()
            })
            .FirstOrDefaultAsync(u => u.Id == request.Id);

        return user;
    }
}
