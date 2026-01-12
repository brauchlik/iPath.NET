namespace iPath.EF.Core.FeatureHandlers.Nodes.Queries;

using DispatchR.Abstractions.Send;
using EF = Microsoft.EntityFrameworkCore.EF;

public static class NodeFilterExtensions
{
    public static IQueryable<Node> ApplySearchString(this IQueryable<Node> q, string s)
    {
        if (!string.IsNullOrEmpty(s))
        {
            s = "%" + s.Trim().Replace("*", "%") + "%";
            q = q.Where(n => (
                EF.Functions.Like(n.Description!.Title, s) ||
                EF.Functions.Like(n.Description!.Subtitle, s) ||
                EF.Functions.Like(n.Description!.Text, s) ||
                EF.Functions.Like(n.Description!.CaseType, s) ||
                EF.Functions.Like(n.Description!.AccessionNo, s)
            ));
        }
        return q;
    }

    public static IQueryable<Node> ApplyNodeFilter(this IQueryable<Node> q, string filter)
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
                    nameof(Node.Description.Title) => q.Where(n => EF.Functions.Like(n.Description!.Title, f.Val)),
                    nameof(Node.Description.Subtitle) => q.Where(n => EF.Functions.Like(n.Description!.Subtitle, f.Val)),
                    nameof(Node.Description.AccessionNo) => q.Where(n => EF.Functions.Like(n.Description!.AccessionNo, f.Val)),
                    nameof(Node.Description.CaseType) => q.Where(n => EF.Functions.Like(n.Description!.CaseType, f.Val)),
                    nameof(Node.Description.Text) => q.Where(n => EF.Functions.Like(n.Description!.Text, f.Val)),
                    _ => q
                };
            }
        }

        return q;
    }

}
