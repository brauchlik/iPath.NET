using iPath.Application.Features.Nodes;
using Microsoft.AspNetCore.Components;
using System.Text.RegularExpressions;

namespace iPath.Blazor.Componenents.Nodes;

public static class NodeExtensions
{
    extension(NodeListDto dto)
    {
        public string? Title => dto?.Description?.Title ?? "[untitled]";
        public string? SubTitle => dto?.Description?.Subtitle;
        public string? AccessionNo => dto?.Description?.AccessionNo;

        public bool IsNew => dto.LastVisit is null;
        public string IsNewIcon => dto.IsNew ? Icons.Material.TwoTone.NewReleases : string.Empty;

        public bool HasNewAnnotation
        {
            get
            {
                if (dto.LastAnnotationDate.HasValue)
                {
                    if (!dto.LastVisit.HasValue)
                        return true;
                    else
                        return dto.LastVisit.Value < dto.LastAnnotationDate.Value;
                }
                return false;
            }
        }
        public string HasNewAnnotationIcon => dto.HasNewAnnotation ? Icons.Material.TwoTone.Comment : string.Empty;
    }




    extension (NodeDto node)
    {
        public string? Title => node?.Description?.Title;
        public string? SubTitle => node?.Description?.Subtitle;
        public string? AccessionNo => node?.Description?.AccessionNo;
        public string? CaseType => node?.Description?.CaseType;
        public string? DescriptionHtml
        {
            get
            {
                var html = node?.Description?.Text ?? "";

                // replace line breaks
                html = html.ReplaceLineEndings("<br />\n");

                // replace links
                html = MakeLink(html);

                return html;
            }
        }
        public MarkupString DescriptionMarkup => (MarkupString)node?.DescriptionHtml;

        public string GalleryCaption => node?.File is null ? "" : node.File.Filename;

        public string? NodeTitle => node?.File is null ? node.Description?.Title : node.File.Filename;



        public string ThumbUrl() 
        {
            if (!string.IsNullOrEmpty(node.File?.ThumbData))
            {
                return $"data:image/jpeg;base64, {node.File.ThumbData}";
            }
            else if (node.ipath2_id.HasValue)
            {
                return $"https://www.ipath-network.com/ipath/image/src/{node.ipath2_id}";
            }

            return "";
        }

        public string FileUrl()
        {
            if (!node.ipath2_id.HasValue)
            {
                return $"/api/v1/nodes/file/{node.Id}";
            }
            else if (node.ipath2_id.HasValue)
            {
                return $"https://www.ipath-network.com/ipath/image/src/{node.ipath2_id}";
            }

            return "";
        }

        public bool IsImage => node.NodeType == "image";

        public string FileIcon()
        {
            if (node.File != null && node.File.MimeType.ToLower().EndsWith("pdf"))
                return Icons.Custom.FileFormats.FilePdf;

            if (node.NodeType.ToLower() == "folder")
                return Icons.Material.Filled.FolderOpen;

            return Icons.Custom.FileFormats.FileDocument;
        }
    }


    public static string MakeLink(string txt)
    {
        txt = Regex.Replace(txt,
                @"((http|ftp|https):\/\/[\w\-_]+(\.[\w\-_]+)+([\w\-\.,@?^=%&amp;:/~\+#]*[\w\-\@?^=%&amp;/~\+#])?)",
                "<a target='_blank' href='Guid'>Guid</a>");
        return txt;
    }
}
