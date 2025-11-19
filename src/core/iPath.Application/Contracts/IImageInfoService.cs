namespace iPath.Application.Contracts;

public interface IImageInfoService
{
    Task<ImageInfo> GetImageInfoAsync(string filename);
}

public record ImageInfo(int width, int height, string thumb);