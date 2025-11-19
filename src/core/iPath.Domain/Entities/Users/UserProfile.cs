namespace iPath.Domain.Entities;

public class UserProfile
{
    // these fields map to the User Entity
    public Guid UserId { get; set; }    
    public string? Username { get; set; }
    public string? Email { get; set; }


    // from there on the fields for the profile
    [JsonPropertyName("familyname"), MaxLength(50)]
    public string? FamilyName { get; set; }

    [JsonPropertyName("firstname"), MaxLength(50)]
    public string? FirstName { get; set; }

    [JsonPropertyName("initials"), MaxLength(3)]
    public string? Initials { get; set; }


    [JsonPropertyName("specialisation")]
    public string? Specialisation { get; set; }

    public ContactDetails ContactDetails { get; set; } = new();


    public static UserProfile AnonymousProfile()
    {
        return new UserProfile
        {
            Username = "anonymous",
            ContactDetails = new () { Address = new() }
        };
    }

    public static UserProfile EmptyProfile(User? user)
    {
        return new UserProfile
        {
            UserId = user is null ? Guid.Empty : user.Id,   
            Username = user?.UserName,
            ContactDetails = new () 
        };
    }
}



