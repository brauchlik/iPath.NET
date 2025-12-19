namespace iPath.Application.Features.Users;

public class UpdateUserPasswordCommand : IRequest<UpdateUserPasswordCommand, Task<Result>>
{
    public Guid UserId { get; init; }
    public string Password { get; init; }
    public bool SetEmailValidated { get; set; }
}
