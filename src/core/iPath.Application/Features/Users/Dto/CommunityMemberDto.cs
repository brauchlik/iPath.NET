namespace iPath.Application.Features.Users;

public record CommunityMemberDto(Guid CommunityId, Guid UserId, eMemberRole Role, bool IsConsultant, string? Communityname = null, string? Username = null);
