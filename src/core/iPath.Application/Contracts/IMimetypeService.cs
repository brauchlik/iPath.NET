namespace iPath.Application.Contracts;

public interface IMimetypeService
{
    bool IsImage(string Filename);
    string GetMimeType(string Filename);
}
