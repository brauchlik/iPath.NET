using System.Text.RegularExpressions;

namespace iPath.Blazor.Componenents.ServiceRequests;

public static class ServiceRequestExtensions
{
    extension(RequestDescription? dto)
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

    extension(ServiceRequestListDto dto)
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




    extension(ServiceRequestDto node)
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

        public string? NodeTitle => node?.File is null ? node.Description?.Title : node.File.Filename;

        public bool ContainsDocumentId(Guid Id) => node.Documents.Any(c => c.Id == Id);

        public string GetDocumentCaption(Guid Id) => node.Documents.FirstOrDefault(c => c.Id == Id)?.GalleryCaption;
    }


    public static string MakeLink(string txt)
    {
        txt = Regex.Replace(txt,
                @"((http|ftp|https):\/\/[\w\-_]+(\.[\w\-_]+)+([\w\-\.,@?^=%&amp;:/~\+#]*[\w\-\@?^=%&amp;/~\+#])?)",
                "<a target='_blank' href='Guid'>Guid</a>");
        return txt;
    }
}
