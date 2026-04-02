using iPath.Application.Contracts;

namespace iPath.API.Services.Email;

public class EmailAttachmentNameSanitizer : IEmailAttachmentNameSanitizer
{
    public string Sanitize(string filename)
    {
        return filename;
    }
}