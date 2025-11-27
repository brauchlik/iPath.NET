using System.ComponentModel.DataAnnotations;

namespace iPath.Application.Features.Users;

public class CreateUserCommand 
    : IRequest<CreateUserCommand, Task<OwnerDto>>
{
    [Required]
    public string Username { get; set; }

    [Required, EmailAddress]
    public string Email { get; set; }

    public CommunityListDto? Community { get; set; }
}
