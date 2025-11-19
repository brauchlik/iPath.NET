//using Microsoft.Extensions.Options;
//using System.Drawing;
//using System.Drawing.Drawing2D;
//using System.Drawing.Imaging;

//namespace iPath.API.Services.Thumbnail;

//public class SystemDrawingImageInfo(IOptions<iPathConfig> opts) : IImageInfoService
//{
//    public async Task<ImageInfo> GetImageInfoAsync(string filename)
//    {  
//        var originalImage = Image.FromFile(filename);
//        var ImageWidth = originalImage.Width;
//        var ImageHeight = originalImage.Height;

//        int thumbWidth = opts.Value.ThumbSize;
//        int thumbHeight = opts.Value.ThumbSize;

//        if (ImageWidth > ImageHeight)
//        {
//            thumbHeight = (int)((float)ImageHeight / ImageWidth * thumbWidth);
//        }
//        else
//        {
//            thumbWidth = (int)((float)ImageWidth / ImageHeight * thumbHeight);
//        }

//        var thumbnail = new Bitmap(thumbWidth, thumbHeight);

//        using (var graphics = Graphics.FromImage(thumbnail))
//        {
//            graphics.CompositingQuality = CompositingQuality.HighQuality;
//            graphics.SmoothingMode = SmoothingMode.HighQuality;
//            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
//            graphics.DrawImage(originalImage, 0, 0, thumbWidth, thumbHeight);
//        }

//        byte[] bytearray;
//        using (MemoryStream ms = new MemoryStream())
//        {
//            thumbnail.Save(ms, ImageFormat.Jpeg);
//            bytearray = ms.ToArray();
//        }

//        var thumbBase64 = Convert.ToBase64String(bytearray);
//        return new ImageInfo(ImageWidth, ImageHeight, thumbBase64);
//    }

    
//}