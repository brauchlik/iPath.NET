using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;

namespace iPath.EF.Core;

public static class QueryOrderExtensions
{
    /// <summary>
    /// Returns a human readable representation of the ordering on the IQueryable, e.g. "Name asc, Age desc".
    /// Works for typical LINQ provider expressions that use OrderBy/OrderByDescending/ThenBy/ThenByDescending with simple member selectors.
    /// </summary>
    public static string ToOrderString<T>(this IQueryable<T> query)
    {
        if (query == null) return string.Empty;

        var parts = new List<string>();
        VisitExpression(query.Expression, parts);
        return parts.Count == 0 ? string.Empty : string.Join(", ", parts);
    }

    private static void VisitExpression(Expression expr, List<string> parts)
    {
        if (expr is MethodCallExpression m)
        {
            // The source argument is normally at position 0. Visit it first so ordering is collected in the original sequence.
            if (m.Arguments.Count > 0)
                VisitExpression(m.Arguments[0], parts);

            var method = m.Method.Name;
            if (method == "OrderBy" || method == "OrderByDescending" ||
                method == "ThenBy" || method == "ThenByDescending")
            {
                var lambda = ExtractLambdaFromArgument(m.Arguments.ElementAtOrDefault(1));
                var memberPath = LambdaToMemberPath(lambda);
                var dir = method.EndsWith("Descending") ? "desc" : "asc";
                parts.Add($"{memberPath} {dir}");
            }
        }
    }

    private static LambdaExpression? ExtractLambdaFromArgument(Expression? arg)
    {
        if (arg == null) return null;

        // Many providers wrap the lambda in a Quote (UnaryExpression)
        if (arg is UnaryExpression ue && ue.NodeType == ExpressionType.Quote)
            return ue.Operand as LambdaExpression;

        return arg as LambdaExpression;
    }

    private static string LambdaToMemberPath(LambdaExpression? lambda)
    {
        if (lambda == null) return "<unknown>";

        Expression body = lambda.Body;

        // Unwrap conversions: e.g. (object)x.Property
        if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            body = unary.Operand;

        // Typical case: MemberExpression (x => x.Prop or x => x.Nav.Prop)
        if (body is MemberExpression member)
        {
            var segments = new Stack<string>();
            Expression? current = member;
            while (current is MemberExpression me)
            {
                segments.Push(me.Member.Name);
                current = me.Expression;
            }
            return string.Join(".", segments);
        }

        // Fallback: render the body expression text so caller still gets something useful
        return body.ToString() ?? "<expr>";
    }
}
