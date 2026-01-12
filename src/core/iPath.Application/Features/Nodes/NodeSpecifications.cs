using System.Linq.Expressions;

namespace iPath.Application.Features.Nodes;


public class NodeIsVisibleSpecifications(Guid? userId) : Specification<Node>
{
    public override Expression<Func<Node, bool>> ToExpression()
    {
        // node is either owned by the current user or it is not draft and also visible
        return (n => (userId.HasValue && n.OwnerId == userId) || (n.Visibility != eNodeVisibility.Private && !n.IsDraft));
    }
}

public class NodeIsPublicSpecifications : Specification<Node>
{
    public override Expression<Func<Node, bool>> ToExpression()
    {
        // node is either owned by the current user or it is not draft and also visible
        return (n => n.Visibility == eNodeVisibility.Private);
    }
}
