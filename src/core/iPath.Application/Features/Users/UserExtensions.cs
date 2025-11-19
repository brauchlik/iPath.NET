namespace iPath.Application.Features.Users;

public static class UserExtensions
{
    public static OwnerDto ToOwnerDto(this User? owner)
        => owner is null ? new OwnerDto(Guid.Empty, string.Empty) : new OwnerDto(owner.Id, owner.UserName);
}
