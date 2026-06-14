using iPath.Application.Features.SyncImport;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Xml;

namespace iPath.API.Services.SyncImport;

public class SyncImportService(
    OldDataService oldDb,
    iPathDbContext newDb,
    UserManager<User> um,
    RoleManager<Role> rm,
    ILogger<SyncImportService> logger)
{
    private readonly Dictionary<int, Guid> _userIds = [];
    private readonly Dictionary<int, Guid> _groupIds = [];
    private readonly Dictionary<int, Guid> _communityIds = [];
    private readonly Dictionary<int, Guid> _nodeIds = [];
    private readonly Dictionary<int, Guid> _docIds = [];
    private Guid _adminUserId;
    private Guid _defaultCommunityId;

    public async Task InitAsync()
    {
        if (!await rm.Roles.AnyAsync())
        {
            await rm.CreateAsync(new Role { Name = "Admin", NormalizedName = "admin" });
            await rm.CreateAsync(new Role { Name = "Moderator", NormalizedName = "moderator" });
            await rm.CreateAsync(new Role { Name = "Developer", NormalizedName = "developer" });
            await rm.CreateAsync(new Role { Name = "Translator", NormalizedName = "translator" });
        }

        await LoadIdMapsAsync();
        _adminUserId = (await newDb.Users.FirstAsync(u => u.NormalizedUserName == "ADMIN")).Id;
        _defaultCommunityId = (await newDb.Communities.FirstAsync()).Id;
    }

    private async Task LoadIdMapsAsync()
    {
        _userIds.Clear(); _groupIds.Clear(); _communityIds.Clear(); _nodeIds.Clear(); _docIds.Clear();

        foreach (var u in await newDb.Users.Where(u => u.ipath2_id.HasValue).ToListAsync())
            _userIds[u.ipath2_id!.Value] = u.Id;
        foreach (var g in await newDb.Groups.Where(g => g.ipath2_id.HasValue).ToListAsync())
            _groupIds[g.ipath2_id!.Value] = g.Id;
        foreach (var c in await newDb.Communities.Where(c => c.ipath2_id.HasValue).ToListAsync())
            _communityIds[c.ipath2_id!.Value] = c.Id;
        foreach (var n in await newDb.ServiceRequests.Where(n => n.ipath2_id.HasValue).ToListAsync())
            _nodeIds[n.ipath2_id!.Value] = n.Id;
        foreach (var d in await newDb.Documents.Where(d => d.ipath2_id.HasValue).ToListAsync())
            _docIds[d.ipath2_id!.Value] = d.Id;

        logger.LogInformation("Loaded {Users} users, {Groups} groups, {Communities} communities, {Nodes} nodes, {Docs} documents",
            _userIds.Count, _groupIds.Count, _communityIds.Count, _nodeIds.Count, _docIds.Count);
    }

    private Guid? MapUserId(int? id) => id.HasValue && _userIds.TryGetValue(id.Value, out var g) ? g : null;
    private Guid? MapGroupId(int? id) => id.HasValue && _groupIds.TryGetValue(id.Value, out var g) ? g : null;
    private Guid? MapCommunityId(int? id) => id.HasValue && _communityIds.TryGetValue(id.Value, out var g) ? g : null;
    private Guid MapNodeId(int id) => _nodeIds.TryGetValue(id, out var g) ? g : (_nodeIds[id] = Guid.CreateVersion7());
    private Guid MapDocId(int id) => _docIds.TryGetValue(id, out var g) ? g : (_docIds[id] = Guid.CreateVersion7());

    private static string? Decode(byte[]? raw) => raw is { Length: > 0 } ? Encoding.UTF8.GetString(raw) : null;

    private static XmlDocument LoadXml(byte[]? raw)
    {
        var doc = new XmlDocument();
        var data = Decode(raw);
        if (string.IsNullOrEmpty(data)) return doc;
        try
        {
            if (data.Contains("&"))
                data = System.Text.RegularExpressions.Regex.Replace(data, "&(?!amp;)", "&amp;");
            doc.LoadXml(data);
        }
        catch { }
        return doc;
    }

    public async Task<List<OldGroupSummary>> GetOldGroupSummariesAsync(CancellationToken ct = default)
    {
        var groups = await oldDb.GetGroupsAsync(ct);
        var summaries = new List<OldGroupSummary>();
        foreach (var g in groups)
        {
            var rootCount = await oldDb.CountRootObjectsAsync(g.Id, ct);
            summaries.Add(new OldGroupSummary
            {
                Id = g.Id,
                Name = g.Name,
                Entered = g.Entered,
                RootNodeCount = rootCount,
            });
        }
        return summaries;
    }

    public async Task<int> SyncGroupAsync(int groupId, CancellationToken ct = default)
    {
        await LoadIdMapsAsync();
        var roots = await oldDb.GetRootObjectsAsync([groupId], ct);
        var rootsToImport = roots.Where(r => !_nodeIds.ContainsKey(r.Id)).ToList();
        logger.LogInformation("Syncing group {GroupId}: {Count} new root objects", groupId, rootsToImport.Count);

        foreach (var o in rootsToImport)
        {
            if (ct.IsCancellationRequested) break;
            await ImportServiceRequestAsync(o, ct);
        }
        await newDb.SaveChangesAsync(ct);

        var parentIds = rootsToImport.Select(r => r.Id).ToHashSet();
        while (parentIds.Any())
        {
            var children = await oldDb.GetChildObjectsAsync(parentIds, ct);
            var newChildren = children.Where(c => !_docIds.ContainsKey(c.Id)).ToList();
            foreach (var o in newChildren)
                await ImportDocumentAsync(o, ct);
            await newDb.SaveChangesAsync(ct);
            parentIds = newChildren.Select(c => c.Id).ToHashSet();
        }

        await LoadIdMapsAsync();
        return rootsToImport.Count;
    }

    private async Task ImportServiceRequestAsync(OldObjectDto o, CancellationToken ct)
    {
        var n = new ServiceRequest
        {
            Id = MapNodeId(o.Id),
            ipath2_id = o.Id,
            CreatedOn = o.Entered.ToUniversalTime(),
            NodeType = o.ObjClass ?? "default",
            OwnerId = MapUserId(o.Sender_id) ?? _adminUserId,
            GroupId = MapGroupId(o.Group_id) ?? throw new Exception($"Group {o.Group_id} not found")
        };
        n.Description = new RequestDescription();
        var xml = LoadXml(o.Data);
        n.Description.Title = xml.SelectSingleNode("/data/title")?.InnerText ?? $"Node #{o.Id}";
        n.Description.Subtitle = xml.SelectSingleNode("/data/subtitle")?.InnerText;
        n.Description.CaseType = xml.SelectSingleNode("/data/type")?.InnerText;
        n.Description.AccessionNo = xml.SelectSingleNode("/data/speciment_code")?.InnerText;
        n.Description.Text = xml.SelectSingleNode("/data/description")?.InnerText;
        newDb.ServiceRequests.Add(n);

        newDb.Set<ServiceRequestImport>().Add(new ServiceRequestImport
        {
            Id = Guid.CreateVersion7(),
            ServiceRequestId = n.Id,
            Info = Decode(o.Info),
            Data = Decode(o.Data)
        });
    }

    private async Task ImportDocumentAsync(OldObjectDto o, CancellationToken ct)
    {
        var n = new DocumentNode
        {
            Id = MapDocId(o.Id),
            ipath2_id = o.Id,
            CreatedOn = o.Entered.ToUniversalTime(),
            DocumentType = o.ObjClass ?? "file",
            OwnerId = MapUserId(o.Sender_id) ?? _adminUserId,
            SortNr = o.Sort_nr ?? 0
        };
        if (o.Parent_id.HasValue)
            n.ParentNodeId = _docIds.TryGetValue(o.Parent_id.Value, out var pid) ? pid : MapNodeId(o.Parent_id.Value);

        var xml = LoadXml(o.Data);
        if (xml.SelectSingleNode("/data/filename") != null)
        {
            n.File ??= new();
            n.File.Filename = xml.SelectSingleNode("/data/filename")!.InnerText;
            n.File.MimeType = xml.SelectSingleNode("/data/mimetype")?.InnerText;
        }
        newDb.Documents.Add(n);

        newDb.Set<DocumentImport>().Add(new DocumentImport
        {
            Id = Guid.CreateVersion7(),
            DocumentId = n.Id,
            Info = Decode(o.Info),
            Data = Decode(o.Data)
        });
    }
}
