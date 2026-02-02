namespace iPath.EF.Core.FeatureHandlers.ServiceRequests.Queries;

using DispatchR.Abstractions.Send;
using EF = Microsoft.EntityFrameworkCore.EF;

public static class NodeFilterExtensions
{
    public static IQueryable<ServiceRequest> ApplySearchString(this IQueryable<ServiceRequest> q, string s)
    {
        if (!string.IsNullOrEmpty(s))
        {
            s = "%" + s.Trim().Replace("*", "%") + "%";
            q = q.Where(n => (
                EF.Functions.Like(n.Description!.Title, s) ||
                EF.Functions.Like(n.Description!.Subtitle, s) ||
                EF.Functions.Like(n.Description!.Text, s) ||
                EF.Functions.Like(n.Description!.CaseType, s) ||
                EF.Functions.Like(n.Description!.AccessionNo, s) ||
                EF.Functions.Like(n.Description!.BodySite!.Display, s) ||
                EF.Functions.Like(n.Description!.BodySite!.Code, s)
            ));

            // maybe search in questionnaire text?
            // || EF.Functions.Like(n.Description!.Questionnaire!.GeneratedText, s)

        }
        return q;
    }

    public static IQueryable<ServiceRequest> ApplyNodeFilter(this IQueryable<ServiceRequest> q, string filter)
    {
        // split string to (colum) (operator) (value)
        var m = System.Text.RegularExpressions.Regex.Match(filter, @"^(.+)\s+(.+)\s+(.*)");
        if (m.Success)
        {
            var f = new { Col = m.Groups[1].Value, Op = m.Groups[2].Value, Val = m.Groups[3].Value.Replace("*", "%") };

            if (f.Op is "=" or "==" or "eq")
            {
                return f.Col switch
                {
                    nameof(ServiceRequest.Description.Title) => q.Where(n => EF.Functions.Like(n.Description!.Title, f.Val)),
                    nameof(ServiceRequest.Description.Subtitle) => q.Where(n => EF.Functions.Like(n.Description!.Subtitle, f.Val)),
                    nameof(ServiceRequest.Description.AccessionNo) => q.Where(n => EF.Functions.Like(n.Description!.AccessionNo, f.Val)),
                    nameof(ServiceRequest.Description.CaseType) => q.Where(n => EF.Functions.Like(n.Description!.CaseType, f.Val)),
                    nameof(ServiceRequest.Description.Text) => q.Where(n => EF.Functions.Like(n.Description!.Text, f.Val)),
                    _ => q
                };
            }
        }

        return q;
    }

}
