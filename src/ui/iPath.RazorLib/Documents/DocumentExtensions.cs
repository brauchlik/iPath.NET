using iPath.Application.Features.Documents;

namespace iPath.Blazor.Componenents.Documents;

public static class DocumentExtensions
{

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
                if (!string.IsNullOrEmpty(document.DocumentType))
                {
                    return document.DocumentType == "image";
                }
                else if(!string.IsNullOrEmpty(document.File?.MimeType))
                {
                    return document.File.MimeType.ToLower().StartsWith("images");
                }
                return false;
            }
        }

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
    }

}
