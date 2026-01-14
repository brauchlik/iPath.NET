using Humanizer;
using iPath.Application.Features.Nodes;
using Microsoft.AspNetCore.Components;
using System.Text.RegularExpressions;

namespace iPath.Blazor.Componenents.Nodes;

public static class NodeExtensions
{
    extension(NodeDescription? dto)
    {
        public string? FullTitle()
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(dto?.Title))
                parts.Add(dto.Title);
            if (!string.IsNullOrEmpty(dto?.AccessionNo))
                parts.Add(dto.AccessionNo);
            if (!parts.Any())
                parts.Add("(No Title)");

            return string.Join(", ", parts);
        }
    }

    extension(NodeListDto dto)
    {


        public string? Title
        {
            get
            {
                var ret = dto.Description.FullTitle();
                if (dto.IsDraft)
                    ret += " (Draft)";
                return ret;
            }
        }            
            
            
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




    extension(NodeDto node)
    {
        public string? Title
        {
            get
            {
                return node.Description.FullTitle();
            }
        }

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

        public bool ContainsChildId(Guid Id) => node.ChildNodes.Any(c => c.Id == Id);


        public string ThumbUrl
        {
            get
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
        }

        public string FileUrl
        {
            get
            {
                if (!node.ipath2_id.HasValue)
                {
                    return $"/api/v1/nodes/file/{node.Id}/{node.File.Filename}";
                }
                else if (node.ipath2_id.HasValue)
                {
                    return $"https://www.ipath-network.com/ipath/image/src/{node.ipath2_id}";
                }

                return "";
            }
        }

        public bool IsImage => node.NodeType == "image";

        public string FileExtension
        {
            get
            {
                if (node is not null && node.File is not null && !string.IsNullOrEmpty(node.File.Filename))
                {
                    var fi = new FileInfo(node.File.Filename);
                    return fi.Extension;
                }
                return string.Empty;
            }
        }

        public string FileIcon
        {
            get
            {
                if (node.FileExtension == ".pdf")
                    return Icons.Custom.FileFormats.FilePdf;

                if (node.FileExtension == ".svs")
                    return Icons.Custom.FileFormats.FileImage;

                if (node.NodeType.ToLower() == "folder")
                    return Icons.Material.Filled.FolderOpen;

                return Icons.Custom.FileFormats.FileDocument;
            }
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
