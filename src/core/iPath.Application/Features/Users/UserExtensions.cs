using DispatchR.Abstractions.Send;
using System.Text.Json;

namespace iPath.Application.Features.Users;

public static class UserExtensions
{
    public static OwnerDto ToOwnerDto(this User? user)
        => user is null ? new OwnerDto(Guid.Empty, string.Empty, string.Empty) : new OwnerDto(user.Id, user.UserName, user.Email);
}
