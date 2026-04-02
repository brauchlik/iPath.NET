namespace iPath.Application.Contracts;

public interface IEmailBodyTextSanitizer
{
    string Sanitize(string? htmlBody, string? plainTextBody);
}