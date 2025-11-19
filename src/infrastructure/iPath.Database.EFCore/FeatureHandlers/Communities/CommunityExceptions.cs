namespace iPath.EF.Core.FeatureHandlers.Communities;

public class CommunityNameExistsException : ArgumentException
{
    public readonly string Name;

    public CommunityNameExistsException(string name) : base("Community {0} exists already", name)
    {
        Name = name;
    }
}