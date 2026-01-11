namespace iPath.API.Authentication;

public class AuthOptions
{
    public bool RequireConfirmedAccount { get; set; } = true;
    public bool RequireUniqueEmail { get; set; } = true;
    public bool ReconfirmExternalEmail { get; set; } = false;

    public string AllowedUserNameCharacters { get; set; } = @"+-.0123456789@ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyzäçèéïöüčėţūŽžơưҲị";


    public GoogleAuthOptions Google { get; set; } = new GoogleAuthOptions();
    public MicrosoftAuthOptions Microsoft { get; set; } = new MicrosoftAuthOptions();
}


public class  GoogleAuthOptions
{
    public bool IsActive { get; set; }
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
}

public class MicrosoftAuthOptions
{
    public bool IsActive { get; set; }
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
}