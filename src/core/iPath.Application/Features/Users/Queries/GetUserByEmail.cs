namespace iPath.Application.Features.Users;

public record GetUserByEmailQuery(string Email) : IRequest<GetUserByEmailQuery, Task<UserDto>>;
