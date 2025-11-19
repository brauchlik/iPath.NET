namespace iPath.Application.Features.Users;

public record GetUserByIdQuery(Guid Id) : IRequest<GetUserByIdQuery, Task<UserDto>>;
