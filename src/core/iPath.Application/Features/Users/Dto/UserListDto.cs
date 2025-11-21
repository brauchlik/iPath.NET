namespace iPath.Application.Features.Users;

public record UserListDto(Guid Id, string Username, string Email, string Initials, bool IsActive, bool EmailConfirmed, string[] Roles);
