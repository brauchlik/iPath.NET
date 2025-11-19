namespace iPath.Application.Features.Users;

public record UserDto
{
    public Guid Id { get; init; }
    public required string Username { get; init; }
    public required string Email { get; init; } 

    public UserProfile Profile { get; init; } = new();

    public ICollection<UserGroupMemberDto> GroupMembership { get; init; } = [];

    public string[] Roles { get; init; } = [];

    public bool IsActive { get; init; }

    public DateTime CreatedOn { get; init; } 
}
