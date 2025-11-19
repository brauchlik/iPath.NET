namespace iPath.Application.Features.Users;

public record UserListDto(Guid Id, string Username, string Email, bool IsActive, bool EmailConfirmed, string[] Roles);
