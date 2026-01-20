using iPath.Application.Features.Documents;
using iPath.Domain.Config;
using Microsoft.Extensions.DependencyInjection;
using System.Drawing;
using Size = System.Drawing.Size;

namespace iPath.Blazor.Componenents.Documents;

public static class DocumentExtensions
{
    private static iPathClientConfig cfg;

    // Call this once at app startup!
    public static void Initialize(IServiceProvider serviceProvider)
    {
        cfg = serviceProvider.GetRequiredService<IOptions<iPathClientConfig>>().Value;
    }


    extension(DocumentDto document)
    {
        public string? Title
        {
            get
            {
                return document.File.Filename;
            }
        }

        public string GalleryCaption => document?.File is null ? "" : document.File.Filename;
               

        public string ThumbUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(document.File?.ThumbData))
                {
                    return $"data:image/jpeg;base64, {document.File.ThumbData}";
                }
                else if (document.ipath2_id.HasValue)
                {
                    return $"https://www.ipath-network.com/ipath/image/src/{document.ipath2_id}";
                }

                return "";
            }
        }

        public string BinarayDataUrl => $"/files/{document.Id}";

        public string FileUrl
        {
            get
            {
                if (!document.ipath2_id.HasValue)
                {
                    return $"/api/v1/documents/{document.Id}/{document.File.Filename}";
                }
                else if (document.ipath2_id.HasValue)
                {
                    return $"https://www.ipath-network.com/ipath/image/src/{document.ipath2_id}";
                }

                return "";
            }
        }

        public bool IsImage
        {
            get
            {
                if (!string.IsNullOrEmpty(document.DocumentType) && document.DocumentType == "image")
                {
                    return true;
                }
                else if(!string.IsNullOrEmpty(document.File?.MimeType) && document.File.MimeType.ToLower().StartsWith("images"))
                {
                    return true;
                }
                return false;
            }
        }

        public bool IsWSI
        {
            get
            {
                if (cfg.WsiExtensions.Any(x => string.Compare(x, document.FileExtension, true) == 0))
                {
                    return true;
                }
                return false;
            }
        }

        public bool IsSlide => document.IsImage || document.IsWSI;

        public string FileExtension
        {
            get
            {
                if (document is not null && document.File is not null && !string.IsNullOrEmpty(document.File.Filename))
                {
                    var fi = new FileInfo(document.File.Filename);
                    return fi.Extension;
                }
                return string.Empty;
            }
        }

        public string FileIcon
        {
            get
            {
                if (document.FileExtension == ".pdf")
                    return Icons.Custom.FileFormats.FilePdf;

                if (document.FileExtension == ".svs")
                    return Icons.Custom.FileFormats.FileImage;

                if (document.DocumentType.ToLower() == "folder")
                    return Icons.Material.Filled.FolderOpen;

                return Icons.Custom.FileFormats.FileDocument;
            }
        }


        public Size? Dimensions => document.File is not null && document.File.ImageHeight.HasValue ?
            new System.Drawing.Size(document.File.ImageWidth.Value, document.File.ImageHeight.Value) :
            null;
    }

}
