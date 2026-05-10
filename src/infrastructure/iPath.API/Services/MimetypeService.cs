using iPath.Application;
using iPath.Application.Contracts;
using Microsoft.AspNetCore.StaticFiles;

namespace iPath.API.Services;

public class MimetypeService : IMimetypeService
{
    private static readonly FileExtensionContentTypeProvider _provider = new();
    private static readonly List<string> ImageExtensions = new List<string> { ".JPG", ".JPEG", ".JPE", ".BMP", ".GIF", ".PNG" };

    public bool IsImage(string Filename)
    {
        var fi = new FileInfo(Filename);
        return ImageExtensions.Contains(fi.Extension.ToUpper());
    }

    public string GetMimeType(string Filename)
    {
        if (_provider.TryGetContentType(Filename, out var mimeType))
        {
            return mimeType;
        }
        return "application/octet-stream";
    }
}
