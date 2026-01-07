namespace iPath.API.Authentication;

public class AuthOptions
{
    public bool RequireConfirmedAccount { get; set; } = true;
    public bool RequireUniqueEmail { get; set; } = true;

    public string AllowedUserNameCharacters { get; set; } = @"+-.0123456789@ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyzäçèéïöüčėţūŽžơưҲị";
}

