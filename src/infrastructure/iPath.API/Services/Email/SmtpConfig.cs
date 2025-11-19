namespace iPath.API.Services.Email;

public class SmtpConfig
{
    public bool Active { get; set; }

    public string SenderName { get; set; } = "iPath Server";
    public string SenderEmail { get; set; } = "";

    public string SmtpServer { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUsername { get; set; } = "";
    public string SmtpPassword { get; set; } = "";
    public bool SmtpTls { get; set; }

}