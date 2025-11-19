using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace iPath.API.Services.Thumbnail;

public class ImageSharpImageInfo(IOptions<iPathConfig> opts) : IImageInfoService
{
    public async Task<iPath.Application.Contracts.ImageInfo> GetImageInfoAsync(string filename)
    {
        using Image originalImage = await Image.LoadAsync(filename);
        var ImageWidth = originalImage.Width;
        var ImageHeight = originalImage.Height;

        int thumbWidth = opts.Value.ThumbSize;
        int thumbHeight = opts.Value.ThumbSize;

        if (ImageWidth > ImageHeight)
        {
            thumbHeight = (int)((float)ImageHeight / ImageWidth * thumbWidth);
        }
        else
        {
            thumbWidth = (int)((float)ImageWidth / ImageHeight * thumbHeight);
        }

        originalImage.Mutate(x => x.Resize(thumbWidth, thumbHeight));
        
        byte[] bytearray;
        using (MemoryStream ms = new MemoryStream())
        {
            await originalImage.SaveAsJpegAsync(ms);
            bytearray = ms.ToArray();
        }

        var thumbBase64 = Convert.ToBase64String(bytearray);
        return new iPath.Application.Contracts.ImageInfo(ImageWidth, ImageHeight, thumbBase64);
    }        
}