using Cyrillic.Convert;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Xml;

namespace iPath.DataImport;


public class UserDTO
{
    public Guid UserId { get; set; }   
    public string Username { get; set; }
}


public static class DataImportExtensions
{ 

    public static Dictionary<int, Guid> userIds = new();
    public static Guid? NewUserId(int? id) => id.HasValue && userIds.ContainsKey(id.Value) ? userIds[id.Value] : null;

    public static Dictionary<int, Guid> groupIds = new();
    public static Guid? NewGroupId(int? id) => id.HasValue && groupIds.ContainsKey(id.Value) ? groupIds[id.Value] : null;

    public static Dictionary<int, Guid> communityIds = new();
    public static Guid? NewCommunityId(int? id) => id.HasValue && communityIds.ContainsKey(id.Value) ? communityIds[id.Value] : null;


    public static Dictionary<int, Guid> nodeIds = new();
    public static Guid? NewNodeId(int? id) => id.HasValue && nodeIds.ContainsKey(id.Value) ? nodeIds[id.Value] : null;

    public static Dictionary<int, Guid> annotationIds = new();


    private static Conversion conv = new Conversion();


    public static async Task InitIdDictAsync(iPathDbContext newDb)
    {
        userIds = await newDb.Users.AsNoTracking()
            .Where(u => u.ipath2_id.HasValue)
            .Select(u => new {u.ipath2_id, u.Id})
            .ToDictionaryAsync(u => u.ipath2_id.Value, u => u.Id);

        groupIds = await newDb.Groups.AsNoTracking()
            .Where(u => u.ipath2_id.HasValue)
            .Select(u => new {u.ipath2_id, u.Id})
            .ToDictionaryAsync(u => u.ipath2_id.Value, u => u.Id);

        communityIds = await newDb.Communities.AsNoTracking()
            .Where(u => u.ipath2_id.HasValue)
            .Select(u => new {u.ipath2_id, u.Id})
            .ToDictionaryAsync(u => u.ipath2_id.Value, u => u.Id);

        nodeIds = await newDb.Nodes.AsNoTracking()
            .Where(u => u.ipath2_id.HasValue)
            .Select(u => new {u.ipath2_id, u.Id})
            .ToDictionaryAsync(u => u.ipath2_id.Value, u => u.Id);

        annotationIds = await newDb.Annotations.AsNoTracking()
            .Where(u => u.ipath2_id.HasValue)
            .Select(u => new {u.ipath2_id, u.Id})
            .ToDictionaryAsync(u => u.ipath2_id.Value, u => u.Id);

        Console.WriteLine("dictionaries loaded");
    }


    public static void CreateNewId(this i2object o)
    {
        if (!nodeIds.ContainsKey(o.id))
        {
            nodeIds.Add(o.id, Guid.CreateVersion7());
        }
        else
        {
            Debug.WriteLine("new node id for #{0} exists already", o.id);
        }
        if (o.ChildNodes != null && o.ChildNodes.Any())
        {
            foreach (var c in o.ChildNodes)
            {
                c.CreateNewId();
            }
        }
    }
    
    public static Node ToNewEntity(this i2object o)
    {
        var n = new Node()
        {
            Id = NewNodeId(o.id).Value,
            ipath2_id = o.id
        };

        n.CreatedOn = o.entered.ToUniversalTime();
        n.NodeType = o.objclass ?? "default";

        var newo = NewUserId(o.sender_id);
        if (newo.HasValue)
        {
            n.OwnerId = newo.Value;
        }
        else
        {
            Console.WriteLine("--");
        }
        n.GroupId = NewGroupId(o.group_id);

        n.RootNodeId = NewNodeId(o.topparent_id);
        n.ParentNodeId = NewNodeId(o.parent_id);

        if (o.parent_id.HasValue && !n.ParentNodeId.HasValue)
        {
            Console.WriteLine("Parent Node {0} not found in import of child {1}", o.parent_id, n.ipath2_id);
        }

        n.SortNr = o.sort_nr;

        // old data and info fields
        /*
        n.ImportedData ??= new();
        n.ImportedData.Data = o.data;
        n.ImportedData.Info = o.info;
        n.LastModifiedOn = o.modified.HasValue ? o.modified.Value.ToUniversalTime() : null;
        */

        try
        {
            var xml = LoadDataDocument(o.data);

            if (xml.SelectSingleNode("/data/title") != null)
            {
                n.Description.Title = xml.SelectSingleNode("/data/title").InnerText;
                n.Description.Subtitle = xml.SelectSingleNode("/data/subtitle")?.InnerText;
                n.Description.CaseType = xml.SelectSingleNode("/data/type")?.InnerText;
                n.Description.AccessionNo = xml.SelectSingleNode("/data/speciment_code")?.InnerText;
                n.Description.Status = xml.SelectSingleNode("/data/casestat")?.InnerText;
                n.Description.Text = xml.SelectSingleNode("/data/description")?.InnerText;
            }


            if (xml.SelectSingleNode("/data/filename") != null)
            {
                n.File ??= new();
                n.File.Filename = xml.SelectSingleNode("/data/filename").InnerText;
                n.File.MimeType = xml.SelectSingleNode("/data/mimetype")?.InnerText;
            }

        }
        catch (Exception ex)
        {
            throw ex;
        }

        return n;
    }




    public static Annotation? ToNewEntity(this i2annotation a)
    {
        var n = new Annotation()
        {
            Id = Guid.CreateVersion7(),
            ipath2_id = a.id
        };

        n.CreatedOn = a.entered.ToUniversalTime();
        var nn = NewUserId(a.sender_id);
        if (nn is null)
        {
            throw new Exception($"Annotation {a.id}: Owner user id {a.sender_id} not found");
        }
        n.OwnerId = nn.Value; 
        n.NodeId = NewNodeId(a.object_id);

        n.Data ??= new AnnotationData();

        try
        {
            var xml = LoadDataDocument(a.data);

            if (xml.SelectSingleNode("/data/text") != null)
            {
                n.Data.Text = xml.SelectSingleNode("/data/text").InnerText;
            }
        }
        catch (Exception ex)
        {
            throw ex;
        }

        return n;
    }

    public static UserDTO ToDto(this User p)
    {
        return new UserDTO() { UserId = p.Id, Username = p.UserName };
    }



    public static HashSet<string> usernames = new();

    private static string CheckUserDuplicate(i2person user)
    {
        var uname = CleanUsername(user.username);
        if (usernames.Contains(uname.ToLowerInvariant()))
        {
            uname = $"{user.id}-{uname}";
        }
        usernames.Add(uname.ToLowerInvariant());

        return uname;
    }


    public static User ToNewEntity(this i2person p)
    {
        var u = new User()
        {
            Id = Guid.CreateVersion7(),
            ipath2_id = p.id,
            ipath2_username = p.username,
            ipath2_password = p.password,
        };
        userIds.Add(p.id, u.Id);

        var username = CheckUserDuplicate(p);
        u.UserName = username;
        u.NormalizedUserName = p.username.ToLowerInvariant().Normalize().Trim();

        u.Email = p.email.Trim();
        u.NormalizedEmail = p.email.ToLowerInvariant().Normalize().Trim();

        u.PasswordHash = "-";

        u.CreatedOn = p.entered.Value.ToUniversalTime();
        u.IsActive = !p.status.HasFlag(eUSERSTAT.INACTIVE);

        // not confirmed 

        try
        {
            var xml = LoadDataDocument(p.data);

            // userid and username are duplicated to profile for easy access in serialized json files
            u.Profile.UserId = u.Id;
            u.Profile.Username = p.username;

            u.Profile.FirstName = xml.SelectSingleNode("/data/firstname")?.InnerText;
            u.Profile.FamilyName = xml.SelectSingleNode("/data/name")?.InnerText;
            u.Profile.Specialisation = xml.SelectSingleNode("/data/specialisation")?.InnerText;


            if (!string.IsNullOrEmpty(u.Profile.FirstName))
            {
                u.Profile.Initials = u.Profile.FirstName.Substring(0, 1);
                u.Profile.Initials += string.IsNullOrEmpty(u.Profile.FamilyName) ? "" : u.Profile.FamilyName.Substring(0, 1);
            }
            else
            {
                u.Profile.Initials = u.UserName.Substring(0, 1);
            }

            var cd = new ContactDetails();

            cd.Organisation = xml.SelectSingleNode("/data/institute")?.InnerText;
            cd.PhoneNr = xml.SelectSingleNode("/data/phone")?.InnerText;
            cd.Email = p.email; // email is duplicated to contact details. maybe be different from email on user account

            cd.Address ??= new();
            cd.Address.Street = xml.SelectSingleNode("/data/street")?.InnerText;
            cd.Address.PostalCode = xml.SelectSingleNode("/data/zip")?.InnerText;
            cd.Address.City = xml.SelectSingleNode("/data/city")?.InnerText;
            cd.Address.Country = xml.SelectSingleNode("/data/country")?.InnerText;

            u.Profile.ContactDetails = cd;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error in XML: " + p.data);
        }

        return u;
    }



    public static Community ToNewEntity(this i2community o)
    {
        var n = new Community
        {
            Id = Guid.CreateVersion7(),
            ipath2_id = o.id,
            Name = o.name,
            CreatedOn = o.created_on.ToUniversalTime()
        };
        communityIds.Add(o.id, n.Id);

        n.OwnerId = NewUserId(o.created_by);
        return n;
    }



    public static Group ToNewEntity(this i2group g)
    {
        var n = new Group()
        {
            Id = Guid.CreateVersion7(),
            ipath2_id = g.id,
            Name = g.name,
            CreatedOn = g.entered.ToUniversalTime()
        };
        groupIds.Add(g.id, n.Id);

        // communities
        foreach (var cg in g.communities)
        {
            if (NewCommunityId(cg.community_id).HasValue)
            {
                n.Communities.Add(new CommunityGroup { Group = n, CommunityId = NewCommunityId(cg.community_id).Value });
            }
        }

        try
        {
            if (!string.IsNullOrEmpty(g.info) && g.info.Contains("&"))
            {
                g.info = System.Text.RegularExpressions.Regex.Replace(g.info, "&(?!amp;)", "&amp;");
            }

            var xml = LoadDataDocument(g.info);

            n.Settings ??= new();
            n.Settings.Purpose = xml.SelectSingleNode("/info/purpose")?.InnerText;
            n.Settings.DescriptionTemplate = xml.SelectSingleNode("/info/default_text")?.InnerText;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error in XML: " + g.info);
        }

        return n;
    }


    public static XmlDocument LoadDataDocument(string data)
    {
        try
        {
            if (!string.IsNullOrEmpty(data))
            {
                if (data.Contains("&"))
                {
                    data = System.Text.RegularExpressions.Regex.Replace(data, "&(?!amp;)", "&amp;");
                }

                var xml = new XmlDocument();
                xml.LoadXml(data);
                return xml;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }

        return new XmlDocument();
    }


    public static string CleanUsername(string orig)
    {
        var username = orig.Trim();
        username = conv.RussianCyrillicToLatin(username);
        username = username.Replace(" ", "_");
        username = username.Replace(",", "_");
        username = username.Replace("[", "_");
        username = username.Replace("]", "_");
        username = username.Replace("/", "_");
        username = username.Replace("+", "_");
        username = username.Replace("'", "_");
        username = username.Replace("=", "_");
        username = username.Replace("?", "_");
        username = username.Replace("&", "_");
        username = username.Replace("\"", "_");

        return username;
    }
}
