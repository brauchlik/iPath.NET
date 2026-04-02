namespace iPath.Application.Contracts;

public interface IEmailAttachmentNameSanitizer
{
    string Sanitize(string filename);
}