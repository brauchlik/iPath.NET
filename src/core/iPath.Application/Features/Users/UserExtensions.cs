using DispatchR.Abstractions.Send;
using System.Text.Json;

namespace iPath.Application.Features.Users;

public static class UserExtensions
{
    public static OwnerDto ToOwnerDto(this User? user)
        => user is null ? new OwnerDto(Guid.Empty, string.Empty, string.Empty) : new OwnerDto(user.Id, user.UserName, user.Email);


    extension (UserProfile profile)
    {
        public bool IsComplete()
        {
            if (string.IsNullOrEmpty(profile.FamilyName)) return false;
            if (string.IsNullOrEmpty(profile.FirstName)) return false;
            if (string.IsNullOrEmpty(profile.Specialisation)) return false;
            return true;
        }
    }

    extension(OwnerDto? owner)
    {
        public string ToLongString() => $"{owner.Username} ({owner.Email})";
    }
}
