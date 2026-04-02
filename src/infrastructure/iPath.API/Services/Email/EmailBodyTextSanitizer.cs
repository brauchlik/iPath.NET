using System.Text.RegularExpressions;
using iPath.Application.Contracts;

namespace iPath.API.Services.Email;

public class EmailBodyTextSanitizer : IEmailBodyTextSanitizer
{
    public string Sanitize(string? htmlBody, string? plainTextBody)
    {
        if (!string.IsNullOrWhiteSpace(plainTextBody))
            return plainTextBody;

        if (string.IsNullOrWhiteSpace(htmlBody))
            return string.Empty;

        return StripHtmlTags(htmlBody);
    }

    private string StripHtmlTags(string html)
    {
        var result = Regex.Replace(html, "<[^>]+>", " ", RegexOptions.Compiled);
        result = Regex.Replace(result, @"\s+", " ", RegexOptions.Compiled);
        return result.Trim();
    }
}