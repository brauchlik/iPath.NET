namespace iPath.EF.Core.FeatureHandlers.Groups;

public class GroupNameExistsException : ArgumentException
{ 
    public readonly string Name;

    public GroupNameExistsException(string name) : base("Group {0} exists already", name)
    {
        Name = name;
    }
}


//public class GroupNotFoundException : NotFoundException
//{
//    public readonly int GroupId;

//    public GroupNotFoundException(int id) : base(id.ToString(), "Group")
//    {
//        GroupId = id;
//    }
//}

