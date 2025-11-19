using Org.BouncyCastle.Asn1.Mozilla;

namespace iPath.Google;

public class GmailConfig
{
    public bool Active { get; set; }
    public string SenderEmail { get; set; }
    public string SenderName { get; set; } = "iPath Server";

    public string AppPassword { get; set; }

    public string SmtpServer { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
}