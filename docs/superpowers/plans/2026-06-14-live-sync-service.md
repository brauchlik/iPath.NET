# Live Sync Service Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a parallel import/sync service that reads from old production MySQL without any database preparation (no encoding fix SQL, no `_top_id`, no PK additions, no FK cleanup).

**Architecture:** Replace EF Core `OldDB : DbContext` with raw MySQL queries via Dapper. Read `data`/`info` columns as binary bytes and decode as UTF-8 in application code. Resolve parent-child relationships via recursive CTE or level-by-level queries instead of `_top_id`. Keep all existing import code untouched.

**Tech Stack:** .NET 10, Dapper, MySqlConnector (or existing MySql.EntityFrameworkCore), existing iPath.Domain entities

**Key design decisions:**
- `data`/`info` read as `byte[]` via `CAST(col AS BINARY)` — bypasses MySQL charset conversion
- UTF-8 decoded in C#: `Encoding.UTF8.GetString(bytes)`
- Parent chain resolved via `WITH RECURSIVE` CTE or iterative level queries
- All FK/orphan issues handled in code (skip missing refs, don't fix the old DB)

---

### File Structure

```
src/
├── core/iPath.Application/Features/SyncImport/
│   └── SyncImportModels.cs            NEW: DTOs & API models
│
├── infrastructure/iPath.API/
│   ├── Services/SyncImport/
│   │   ├── OldDataService.cs             NEW: raw MySQL reader (Dapper)
│   │   └── SyncImportService.cs          NEW: orchestrator with progress tracking
│   ├── Endpoints/
│   │   └── SyncImportEndpoints.cs        NEW: GET groups, POST sync
│   └── iPath.API.csproj                 MODIFIED: +Dapper +MySqlConnector
│
├── ui/iPath.RazorLib/Admin/SyncImport/
│   ├── _Imports.razor                   NEW
│   └── SyncImportPage.razor             NEW: group list + sync trigger
│
├── ui/iPath.RazorLib/Admin/AdminNavMenu.razor   MODIFIED: add nav link
└── ui/iPath.Blazor.Server/appsettings.json     MODIFIED: add SyncImport config
```

Key differences from console-only plan:
- Shared code lives in `iPath.API/Services/SyncImport/` (referenced by both web app and console tool)
- `iPath.API` adds Dapper + MySqlConnector packages
- New Blazor admin page at `/admin/sync-import`
- Console tool project `import/DataImport/` stays untouched

---

### Task 1: Add packages to iPath.API

**Files:**
- Modify: `src/infrastructure/iPath.API/iPath.API.csproj`

- [ ] **Step 1: Add Dapper and MySqlConnector**

```xml
<PackageReference Include="Dapper" Version="2.1.66" />
<PackageReference Include="MySqlConnector" Version="2.4.0" />
```

---

### Task 2: Create SyncImportModels.cs — DTOs & API models

**Files:**
- Create: `src/core/iPath.Application/Features/SyncImport/SyncImportModels.cs`

Create `import/DataImport/OldDataService.cs` with DTOs for raw MySQL reads:

```csharp
using Dapper;
using MySql.Data.MySqlClient;
using System.Data;

namespace iPath.DataImport;

public class OldPersonDto
{
    public int Id { get; set; }
    public string Email { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public int? Confirmed { get; set; }
    public int Status { get; set; }
    public int? Creator { get; set; }
    public string Language { get; set; }
    public DateTime? Entered { get; set; }
    public DateTime? Modified { get; set; }
    public DateTime? Lastemail { get; set; }
    public byte[]? Data { get; set; }
    public byte[]? Info { get; set; }
    public int? Default_community { get; set; }
    public byte? Deleted { get; set; }
}

public class OldAnnotationDto
{
    public int Id { get; set; }
    public int Sender_id { get; set; }
    public int? Object_id { get; set; }
    public byte[]? Data { get; set; }
    public DateTime Entered { get; set; }
}

public class OldCommunityDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string? Base_url { get; set; }
    public string? Description { get; set; }
    public int? Created_by { get; set; }
    public DateTime Created_on { get; set; }
}

public class OldCommunityGroupDto
{
    public int Community_id { get; set; }
    public int Group_id { get; set; }
}

public class OldGroupDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int? Type { get; set; }
    public int? Status { get; set; }
    public byte[]? Info { get; set; }
    public DateTime Entered { get; set; }
}

public class OldGroupMemberDto
{
    public int Id { get; set; }
    public int Group_id { get; set; }
    public int User_id { get; set; }
    public int? Status { get; set; }
    public int? Sendmail { get; set; }
    public DateTime? Entered { get; set; }
}

public class OldObjectDto
{
    public int Id { get; set; }
    public string? ObjClass { get; set; }
    public byte[]? Data { get; set; }
    public byte[]? Info { get; set; }
    public DateTime Entered { get; set; }
    public DateTime? Modified { get; set; }
    public int? Group_id { get; set; }
    public int? Parent_id { get; set; }
    public int? Sender_id { get; set; }
    public int? Sort_nr { get; set; }
}

public class OldLastVisitDto
{
    public int Id { get; set; }
    public int User_id { get; set; }
    public int Object_id { get; set; }
    public DateTime Visitdate { get; set; }
}
```

- [ ] **Step 3: Create `OldDataService` class**

Add below the DTOs in the same file:

```csharp
public class OldDataService
{
    private readonly string _connectionString;

    public OldDataService(string connectionString)
    {
        _connectionString = connectionString;
    }

    private IDbConnection CreateConnection()
    {
        return new MySqlConnection(_connectionString);
    }

    public async Task<List<OldPersonDto>> GetPersonsAsync(HashSet<int> excludeIds, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = "SELECT id, email, username, password, confirmed, status, creator, language, " +
                  "entered, modified, lastemail, " +
                  "CAST(data AS BINARY) AS data, CAST(info AS BINARY) AS info, " +
                  "default_community, deleted FROM person";
        var result = await conn.QueryAsync<OldPersonDto>(sql);
        return result.Where(p => !excludeIds.Contains(p.Id)).ToList();
    }

    public async Task<List<OldGroupDto>> GetGroupsAsync(HashSet<int> excludeIds, int? minId = null, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = "SELECT id, name, type, status, CAST(info AS BINARY) AS info, entered FROM groups";
        if (minId.HasValue)
            sql += " WHERE id > @minId";
        var result = await conn.QueryAsync<OldGroupDto>(sql, new { minId });
        return result.Where(g => !excludeIds.Contains(g.Id)).ToList();
    }

    public async Task<List<OldGroupMemberDto>> GetGroupMembersAsync(CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = "SELECT id, group_id, user_id, status, sendmail, entered FROM group_member";
        return (await conn.QueryAsync<OldGroupMemberDto>(sql)).ToList();
    }

    public async Task<List<OldCommunityDto>> GetCommunitiesAsync(CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = "SELECT id, name, base_url, description, created_by, created_on FROM community";
        return (await conn.QueryAsync<OldCommunityDto>(sql)).ToList();
    }

    public async Task<List<OldCommunityGroupDto>> GetCommunityGroupsAsync(CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = "SELECT community_id, group_id FROM community_group";
        return (await conn.QueryAsync<OldCommunityGroupDto>(sql)).ToList();
    }

    public async Task<List<OldObjectDto>> GetRootObjectsAsync(HashSet<int> groupIds, HashSet<int> excludeIds, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = "SELECT id, class AS ObjClass, " +
                  "CAST(data AS BINARY) AS data, CAST(info AS BINARY) AS info, " +
                  "entered, modified, group_id, parent_id, sender_id, sort_nr " +
                  "FROM objects " +
                  "WHERE class != 'imic' " +
                  "AND parent_id IS NULL " +
                  "AND group_id IS NOT NULL " +
                  "AND group_id > 0 " +
                  "AND sender_id IS NOT NULL AND sender_id > 0 " +
                  "AND group_id IN @groupIds";
        var result = await conn.QueryAsync<OldObjectDto>(sql, new { groupIds });
        return result.Where(o => !excludeIds.Contains(o.Id)).ToList();
    }

    public async Task<List<OldObjectDto>> GetChildObjectsAsync(HashSet<int> parentIds, HashSet<int> excludeIds, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = "SELECT id, class AS ObjClass, " +
                  "CAST(data AS BINARY) AS data, CAST(info AS BINARY) AS info, " +
                  "entered, modified, group_id, parent_id, sender_id, sort_nr " +
                  "FROM objects " +
                  "WHERE parent_id IS NOT NULL " +
                  "AND parent_id > 0 " +
                  "AND parent_id IN @parentIds";
        var result = await conn.QueryAsync<OldObjectDto>(sql, new { parentIds });
        return result.Where(o => !excludeIds.Contains(o.Id)).ToList();
    }

    public async Task<List<OldAnnotationDto>> GetAnnotationsAsync(HashSet<int> objectIds, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = "SELECT id, sender_id, object_id, CAST(data AS BINARY) AS data, entered FROM annotation WHERE object_id IN @objectIds";
        return (await conn.QueryAsync<OldAnnotationDto>(sql, new { objectIds })).ToList();
    }

    public async Task<List<OldLastVisitDto>> GetLastVisitsAsync(CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = "SELECT id, user_id, object_id, visitdate FROM lastvisit WHERE user_id > 0 AND object_id > 0";
        return (await conn.QueryAsync<OldLastVisitDto>(sql)).ToList();
    }
}
```

- [ ] **Step 4: Commit**

```bash
git add import/DataImport/OldDataService.cs import/DataImport/iPath.DataImport.csproj
git commit -m "feat: add OldDataService with raw MySQL reader for live sync"
```

---

### Task 2: Create SyncImportModels.cs — DTOs & API models

**Files:**
- Create: `src/core/iPath.Application/Features/SyncImport/SyncImportModels.cs`

- [ ] **Step 1: Create models file**

```csharp
namespace iPath.Application.Features.SyncImport;

// --- Old MySQL DTOs (read as binary, decode in C#) ---

public class OldPersonDto
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public int Status { get; set; }
    public int? Creator { get; set; }
    public DateTime? Entered { get; set; }
    public byte[]? Data { get; set; }
    public byte[]? Info { get; set; }
}

public class OldCommunityDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int? Created_by { get; set; }
    public DateTime Created_on { get; set; }
}

public class OldCommunityGroupDto
{
    public int Community_id { get; set; }
    public int Group_id { get; set; }
}

public class OldGroupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public byte[]? Info { get; set; }
    public DateTime Entered { get; set; }
}

public class OldGroupMemberDto
{
    public int Group_id { get; set; }
    public int User_id { get; set; }
    public int? Status { get; set; }
}

public class OldObjectDto
{
    public int Id { get; set; }
    public string? ObjClass { get; set; }
    public byte[]? Data { get; set; }
    public byte[]? Info { get; set; }
    public DateTime Entered { get; set; }
    public DateTime? Modified { get; set; }
    public int? Group_id { get; set; }
    public int? Parent_id { get; set; }
    public int? Sender_id { get; set; }
    public int? Sort_nr { get; set; }
}

// --- API Models ---

public class OldGroupSummary
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime Entered { get; set; }
    public int RootNodeCount { get; set; }
    public int UserCount { get; set; }
}

public record SyncStartRequest(int GroupId);

public record SyncStartResponse(string JobId);

public record SyncJobStatus(string JobId, bool IsRunning, int Progress, string? Error);
```

---

### Task 3: Create OldDataService.cs — raw MySQL reader

**Files:**
- Create: `src/infrastructure/iPath.API/Services/SyncImport/OldDataService.cs`

- [ ] **Step 1: Create OldDataService**

```csharp
using Dapper;
using iPath.Application.Features.SyncImport;
using MySqlConnector;
using System.Data;

namespace iPath.API.Services.SyncImport;

public class OldDataService(string connectionString)
{
    private IDbConnection CreateConnection() => new MySqlConnection(connectionString);

    public async Task<List<OldGroupDto>> GetGroupsAsync(CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = "SELECT id, name, CAST(info AS BINARY) AS info, entered FROM groups";
        return (await conn.QueryAsync<OldGroupDto>(sql)).ToList();
    }

    public async Task<List<OldGroupMemberDto>> GetGroupMembersAsync(CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = "SELECT group_id, user_id, status FROM group_member";
        return (await conn.QueryAsync<OldGroupMemberDto>(sql)).ToList();
    }

    public async Task<List<OldCommunityDto>> GetCommunitiesAsync(CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = "SELECT id, name, description, created_by, created_on FROM community";
        return (await conn.QueryAsync<OldCommunityDto>(sql)).ToList();
    }

    public async Task<List<OldCommunityGroupDto>> GetCommunityGroupsAsync(CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = "SELECT community_id, group_id FROM community_group";
        return (await conn.QueryAsync<OldCommunityGroupDto>(sql)).ToList();
    }

    public async Task<List<OldPersonDto>> GetPersonsAsync(CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = "SELECT id, email, username, password, status, creator, entered, " +
                  "CAST(data AS BINARY) AS data, CAST(info AS BINARY) AS info FROM person";
        return (await conn.QueryAsync<OldPersonDto>(sql)).ToList();
    }

    public async Task<List<OldObjectDto>> GetRootObjectsAsync(HashSet<int> groupIds, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = "SELECT id, class AS ObjClass, " +
                  "CAST(data AS BINARY) AS data, CAST(info AS BINARY) AS info, " +
                  "entered, modified, group_id, parent_id, sender_id, sort_nr " +
                  "FROM objects " +
                  "WHERE class != 'imic' " +
                  "AND parent_id IS NULL " +
                  "AND group_id IS NOT NULL AND group_id > 0 " +
                  "AND sender_id IS NOT NULL AND sender_id > 0 " +
                  "AND group_id IN @groupIds";
        return (await conn.QueryAsync<OldObjectDto>(sql, new { groupIds })).ToList();
    }

    public async Task<List<OldObjectDto>> GetChildObjectsAsync(HashSet<int> parentIds, CancellationToken ct = default)
    {
        if (!parentIds.Any()) return [];

        using var conn = CreateConnection();
        var sql = "SELECT id, class AS ObjClass, " +
                  "CAST(data AS BINARY) AS data, CAST(info AS BINARY) AS info, " +
                  "entered, modified, group_id, parent_id, sender_id, sort_nr " +
                  "FROM objects " +
                  "WHERE parent_id IS NOT NULL AND parent_id > 0 " +
                  "AND parent_id IN @parentIds";
        return (await conn.QueryAsync<OldObjectDto>(sql, new { parentIds })).ToList();
    }

    public async Task<int> CountRootObjectsAsync(int groupId, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = "SELECT COUNT(*) FROM objects " +
                  "WHERE parent_id IS NULL AND group_id = @groupId AND sender_id > 0";
        return await conn.ExecuteScalarAsync<int>(sql, new { groupId });
    }

    public async Task<int> CountUsersAsync(CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM person WHERE id > 0");
    }
}
```

---

### Task 4: Create SyncImportService.cs — orchestrator

**Files:**
- Create: `src/infrastructure/iPath.API/Services/SyncImport/SyncImportService.cs`

- [ ] **Step 1: Create SyncImportService**

```csharp
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

    // --- Public API ---

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
                UserCount = g.Name.Contains("admin") ? 0 : rootCount // placeholder
            });
        }
        return summaries;
    }

    public async Task<int> SyncGroupAsync(int groupId, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        await LoadIdMapsAsync();
        var roots = await oldDb.GetRootObjectsAsync([groupId], ct);
        var rootsToImport = roots.Where(r => !_nodeIds.ContainsKey(r.Id)).ToList();
        logger.LogInformation("Syncing group {GroupId}: {Count} new root objects", groupId, rootsToImport.Count);

        var count = 0;
        foreach (var o in rootsToImport)
        {
            if (ct.IsCancellationRequested) break;
            await ImportServiceRequestAsync(o, ct);
            count++;
            progress?.Report(count * 100 / rootsToImport.Count);
        }
        await newDb.SaveChangesAsync(ct);

        // Import child documents level by level
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
        n.Description = new ServiceRequestDescription();
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
```

---

### Task 5: Create SyncImportEndpoints.cs

**Files:**
- Create: `src/infrastructure/iPath.API/Endpoints/SyncImportEndpoints.cs`

- [ ] **Step 1: Create endpoints**

```csharp
using iPath.API.Services.SyncImport;
using iPath.Application.Features.SyncImport;
using Microsoft.AspNetCore.Http.Timeouts;

namespace iPath.API;

public static class SyncImportEndpoints
{
    public static IEndpointRouteBuilder MapSyncImportApi(this IEndpointRouteBuilder route)
    {
        var sync = route.MapGroup("admin/sync-import")
            .WithTags("Sync Import")
            .RequireAuthorization("Admin");

        sync.MapGet("groups", async (
            SyncImportService service,
            CancellationToken ct) =>
        {
            var groups = await service.GetOldGroupSummariesAsync(ct);
            return TypedResults.Ok(groups);
        }).Produces<List<OldGroupSummary>>();

        sync.MapPost("sync", [RequestTimeout(milliseconds: 300000)] async (
            SyncStartRequest request,
            SyncImportService service,
            CancellationToken ct) =>
        {
            var count = await service.SyncGroupAsync(request.GroupId, cancellationToken: ct);
            return TypedResults.Ok(new SyncStartResponse($"Synced {count} nodes"));
        }).Produces<SyncStartResponse>();

        return route;
    }
}
```

---

### Task 6: Register endpoints and DI in iPath.API

**Files:**
- Modify: `src/infrastructure/iPath.API/MapEndpoints.cs`
- Modify: `src/infrastructure/iPath.API/Program.cs` (or equivalent service registration file)

- [ ] **Step 1: Find service registration file**

Look for where iPath.API registers its services (likely `Program.cs` in iPath.Blazor.Server, or a service extension in iPath.API). Check what pattern is used.

- [ ] **Step 2: Register SyncImportService + OldDataService in DI**

Add to the service registration:
```csharp
// Sync Import (old MySQL)
var syncCs = builder.Configuration.GetConnectionString("ipath_old");
if (!string.IsNullOrEmpty(syncCs))
{
    builder.Services.AddSingleton(new OldDataService(syncCs));
    builder.Services.AddScoped<SyncImportService>();
}
```

- [ ] **Step 3: Register endpoints in MapEndpoints.cs**

```csharp
.MapSyncImportApi()
```
Add after `.MapEmailImportApi()` in the chain.

---

### Task 7: Create Blazor admin page

**Files:**
- Create: `src/ui/iPath.RazorLib/Admin/SyncImport/_Imports.razor`
- Create: `src/ui/iPath.RazorLib/Admin/SyncImport/SyncImportPage.razor`
- Modify: `src/ui/iPath.RazorLib/Admin/AdminNavMenu.razor`

- [ ] **Step 1: Create _Imports.razor**

```razor
@using iPath.Application.Features.SyncImport
```

- [ ] **Step 2: Create SyncImportPage.razor**

```razor
@page "/admin/sync-import"
@attribute [Authorize(Roles = "Admin")]

@using iPath.Application.Features.SyncImport
@inject IPathApi api
@inject ISnackbar snackbar
@inject IDialogService dialog

<MudText Typo="Typo.h5" Class="mb-4">Sync Import (from old iPath2)</MudText>

<MudAlert Severity="Severity.Warning" Class="mb-4" Dense="true">
    Imports data from the old production MySQL. Only new items (not yet imported) are synced.
    Existing data is never modified.
</MudAlert>

@if (_isLoading)
{
    <MudProgressLinear Indeterminate="true" Color="Color.Primary" />
    <MudText Class="mt-2">Loading groups from old system...</MudText>
}
else if (_groups.Count == 0)
{
    <MudAlert Severity="Severity.Info">No groups found in old system, or old MySQL is unreachable.</MudAlert>
}
else
{
    <MudDataGrid T="OldGroupSummary"
                 Items="@_groups"
                 Dense="true"
                 Hover="true">
        <Columns>
            <PropertyColumn Property="x => x.Id" Title="ID" />
            <PropertyColumn Property="x => x.Name" Title="Group Name" />
            <PropertyColumn Property="x => x.RootNodeCount" Title="Root Nodes" />
            <PropertyColumn Property="x => x.Entered" Title="Created">
                <CellTemplate>
                    @context.Item.Entered.ToString("yyyy-MM-dd")
                </CellTemplate>
            </PropertyColumn>
            <TemplateColumn>
                <CellTemplate>
                    <MudIconButton Icon="@Icons.Material.Filled.Sync"
                                   Size="Size.Small"
                                   Color="Color.Primary"
                                   Disabled="@_syncingGroups.Contains(context.Item.Id)"
                                   OnClick="@(() => StartSync(context.Item))" />
                    @if (_syncingGroups.Contains(context.Item.Id))
                    {
                        <MudCircularProgress Indeterminate="true" Size="Size.Small" Class="ml-2" />
                    }
                </CellTemplate>
            </TemplateColumn>
        </Columns>
    </MudDataGrid>
}

@code {
    private List<OldGroupSummary> _groups = [];
    private HashSet<int> _syncingGroups = [];
    private bool _isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var response = await api.GetAsync<List<OldGroupSummary>>("admin/sync-import/groups");
            if (response.IsSuccessful && response.Content != null)
                _groups = response.Content;
        }
        catch (Exception ex)
        {
            snackbar.Add($"Failed to load groups: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task StartSync(OldGroupSummary group)
    {
        var confirmed = await dialog.ShowMessageBoxAsync("Sync Group",
            $"Import all nodes from group \"{group.Name}\" ({group.RootNodeCount} roots)?",
            yesText: "Sync", cancelText: "Cancel");
        if (confirmed != true) return;

        _syncingGroups.Add(group.Id);
        try
        {
            var response = await api.PostAsync<SyncStartRequest, SyncStartResponse>(
                "admin/sync-import/sync", new SyncStartRequest(group.Id));
            if (response.IsSuccessful)
                snackbar.Add($"Sync complete: {response.Content?.JobId}", Severity.Success);
            else
                snackbar.Add("Sync failed", Severity.Error);
        }
        catch (Exception ex)
        {
            snackbar.Add($"Error: {ex.Message}", Severity.Error);
        }
        finally
        {
            _syncingGroups.Remove(group.Id);
        }
    }
}
```

- [ ] **Step 3: Add nav link in AdminNavMenu.razor**

Add under the Admin section (after the Database link):
```razor
<MudNavLink Href="admin/sync-import" Icon="@Icons.Material.Filled.CloudDownload">Sync Import</MudNavLink>
```

---

### Task 8: Verify build

- [ ] **Step 1: Build the project**

```bash
dotnet build src/infrastructure/iPath.API/iPath.API.csproj
```

Expected: Clean build with no errors.

```bash
git add import/DataImport/SyncExtensions.cs
git commit -m "feat: add SyncExtensions with byte[] decoding and entity conversion"
```

---

### Task 3: Create `SyncConfig.cs` and `syncsettings.json`

**Files:**
- Create: `import/DataImport/SyncConfig.cs`
- Create: `import/DataImport/syncsettings.json`

- [ ] **Step 1: Create SyncConfig class**

```csharp
namespace iPath.DataImport;

public class SyncConfig
{
    public int BulkSize { get; set; } = 10000;
    public bool ImportUsers { get; set; }
    public bool ImportCommunities { get; set; }
    public bool ImportGroups { get; set; }
    public bool ImportServiceRequests { get; set; }
    public bool ImportDocuments { get; set; }
    public bool ImportVisitStats { get; set; }
    public string OldConnectionString { get; set; } = "";
}
```

- [ ] **Step 2: Create syncsettings.json**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database": "Warning"
    }
  },
  "DbProvider": "Sqlite",
  "ConnectionStrings": {
    "Postgres": "Host=127.0.0.1:5433;Database=ipath_data;Username=postgres;Password=test1234;Include Error Detail=true;",
    "Sqlite": "Data Source=C:/Daten/ipath_sqlite/ipath_data.db"
  },
  "SyncConfig": {
    "BulkSize": 10000,
    "ImportUsers": false,
    "ImportCommunities": false,
    "ImportGroups": false,
    "ImportServiceRequests": false,
    "ImportDocuments": false,
    "ImportVisitStats": false,
    "OldConnectionString": "Server=127.0.0.1;Database=ipath2;Uid=ipath;Pwd=1cePath;"
  }
}
```

Note: No `Charset=latin1` in the connection string — encoding is handled in `SyncExtensions.DecodeData()` via `CAST(col AS BINARY)`.

- [ ] **Step 3: Commit**

```bash
git add import/DataImport/SyncConfig.cs import/DataImport/syncsettings.json
git commit -m "feat: add SyncConfig and syncsettings.json"
```

---

### Task 4: Create `SyncService.cs` — orchestrator

**Files:**
- Create: `import/DataImport/SyncService.cs`

Main sync orchestrator. Parallel to `ImportService.cs` but uses `OldDataService` instead of `OldDB`, and `SyncExtensions` instead of `DataImportExtensions`.

- [ ] **Step 1: Create SyncService class**

```csharp
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace iPath.DataImport;

public class SyncService(
    OldDataService oldDb,
    iPathDbContext newDb,
    UserManager<User> um,
    RoleManager<Role> rm)
{
    public int BulkSize { get; set; } = 10000;
    public Guid AdminUserId { get; private set; }
    public Guid DefaultCommunityId { get; private set; }

    public async Task InitAsync()
    {
        if (!await rm.Roles.AnyAsync())
        {
            await rm.CreateAsync(new Role { Name = "Admin", NormalizedName = "admin" });
            await rm.CreateAsync(new Role { Name = "Moderator", NormalizedName = "moderator" });
            await rm.CreateAsync(new Role { Name = "Developer", NormalizedName = "developer" });
            await rm.CreateAsync(new Role { Name = "Translator", NormalizedName = "translator" });
            Console.WriteLine("roles created");
        }

        await LoadIdMapsAsync();
        AdminUserId = (await newDb.Users.FirstAsync(u => u.NormalizedUserName == "ADMIN")).Id;
        DefaultCommunityId = (await newDb.Communities.FirstAsync()).Id;
    }

    private async Task LoadIdMapsAsync()
    {
        SyncExtensions.NewUserIds.Clear();
        SyncExtensions.NewGroupIds.Clear();
        SyncExtensions.NewCommunityIds.Clear();
        SyncExtensions.NewNodeIds.Clear();
        SyncExtensions.NewDocIds.Clear();

        foreach (var u in await newDb.Users.Where(u => u.ipath2_id.HasValue).ToListAsync())
            SyncExtensions.NewUserIds[u.ipath2_id!.Value] = u.Id;

        foreach (var g in await newDb.Groups.Where(g => g.ipath2_id.HasValue).ToListAsync())
            SyncExtensions.NewGroupIds[g.ipath2_id!.Value] = g.Id;

        foreach (var c in await newDb.Communities.Where(c => c.ipath2_id.HasValue).ToListAsync())
            SyncExtensions.NewCommunityIds[c.ipath2_id!.Value] = c.Id;

        foreach (var n in await newDb.ServiceRequests.Where(n => n.ipath2_id.HasValue).ToListAsync())
            SyncExtensions.NewNodeIds[n.ipath2_id!.Value] = n.Id;

        foreach (var d in await newDb.Documents.Where(d => d.ipath2_id.HasValue).ToListAsync())
            SyncExtensions.NewDocIds[d.ipath2_id!.Value] = d.Id;

        Console.WriteLine($"Loaded {SyncExtensions.NewUserIds.Count} users, {SyncExtensions.NewGroupIds.Count} groups, " +
                          $"{SyncExtensions.NewCommunityIds.Count} communities, {SyncExtensions.NewNodeIds.Count} nodes, " +
                          $"{SyncExtensions.NewDocIds.Count} documents");
    }

    public async Task ImportUsersAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var existingIds = SyncExtensions.NewUserIds.Keys.ToHashSet();
        var users = await oldDb.GetPersonsAsync(existingIds, ct);
        Console.WriteLine($"Importing {users.Count} new users...");

        var count = 0;
        foreach (var u in users)
        {
            if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

            try
            {
                var newUser = u.ToNewUser(AdminUserId);
                var result = await um.CreateAsync(newUser);
                if (!result.Succeeded)
                {
                    Console.WriteLine($"Failed to create user {u.Username}: {result.Errors.First().Description}");
                    continue;
                }

                if (newUser.ipath2_id == 1)
                {
                    var adminRole = await rm.FindByNameAsync("Admin");
                    await um.AddToRoleAsync(newUser, adminRole.Name);
                }

                if ((u.Status & 4) != 0) // LANGEDIT
                {
                    var translatorRole = await rm.FindByNameAsync("Translator");
                    await um.AddToRoleAsync(newUser, translatorRole.Name);
                }

                count++;
                if (count % 100 == 0)
                    Console.WriteLine($"{count}/{users.Count} users imported");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing user {u.Username} (#{u.Id}): {ex.Message}");
            }
        }

        Console.WriteLine($"Imported {count} users in {sw.Elapsed.TotalSeconds:F1}s");
        await LoadIdMapsAsync();
    }

    public async Task ImportCommunitiesAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var communities = await oldDb.GetCommunitiesAsync(ct);
        var existingIds = SyncExtensions.NewCommunityIds.Keys.ToHashSet();
        var newCommunities = communities.Where(c => !existingIds.Contains(c.Id)).ToList();

        Console.WriteLine($"Importing {newCommunities.Count} communities...");
        foreach (var c in newCommunities)
        {
            var n = c.ToNewCommunity(AdminUserId);
            newDb.Communities.Add(n);
        }
        await newDb.SaveChangesAsync(ct);
        Console.WriteLine($"Done in {sw.Elapsed.TotalSeconds:F1}s");

        await LoadIdMapsAsync();
    }

    public async Task ImportGroupsAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var groups = await oldDb.GetGroupsAsync(SyncExtensions.NewGroupIds.Keys.ToHashSet(), ct: ct);
        var members = await oldDb.GetGroupMembersAsync(ct);
        var communityLinks = await oldDb.GetCommunityGroupsAsync(ct);

        Console.WriteLine($"Importing {groups.Count} groups...");
        foreach (var g in groups)
        {
            var n = g.ToNewGroup(AdminUserId, DefaultCommunityId, communityLinks);

            // Add members
            foreach (var m in members.Where(m => m.Group_id == g.Id))
            {
                var userId = SyncExtensions.MapUserId(m.User_id);
                if (userId.HasValue && !n.Members.Any(mm => mm.UserId == userId.Value))
                {
                    eMemberRole role = eMemberRole.User;
                    if ((m.Status & 4) != 0) role = eMemberRole.Moderator;
                    if ((m.Status & 2) != 0) role = eMemberRole.Banned;
                    if ((m.Status & 8) != 0) role = eMemberRole.Guest;
                    n.AddMember(userId.Value, role);
                }
            }

            newDb.Groups.Add(n);
        }

        await newDb.SaveChangesAsync(ct);
        Console.WriteLine($"Done in {sw.Elapsed.TotalSeconds:F1}s");

        await LoadIdMapsAsync();
    }

    public async Task ImportServiceRequestsAsync(HashSet<int> groupIds, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var roots = await oldDb.GetRootObjectsAsync(groupIds, SyncExtensions.NewNodeIds.Keys.ToHashSet(), ct);
        Console.WriteLine($"Importing {roots.Count} root objects as ServiceRequests...");

        var requestBulk = new List<ServiceRequest>();
        var importDataBulk = new List<ServiceRequestImport>();
        var annotationBulk = new List<Annotation>();
        var count = 0;

        foreach (var o in roots)
        {
            if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

            var n = o.ToServiceRequest(AdminUserId);
            requestBulk.Add(n);

            var dataStr = SyncExtensions.DecodeData(o.Data);
            var infoStr = SyncExtensions.DecodeData(o.Info);
            importDataBulk.Add(new ServiceRequestImport
            {
                Id = Guid.CreateVersion7(),
                ServiceRequestId = n.Id,
                Info = infoStr,
                Data = dataStr
            });

            count++;
            if (count % 100 == 0)
                Console.WriteLine($"{count}/{roots.Count} ServiceRequests");

            if (count % BulkSize == 0)
            {
                await BulkSaveAsync(requestBulk, importDataBulk, annotationBulk, ct);
                requestBulk.Clear();
                importDataBulk.Clear();
                annotationBulk.Clear();
            }
        }

        if (requestBulk.Any())
            await BulkSaveAsync(requestBulk, importDataBulk, annotationBulk, ct);

        Console.WriteLine($"Imported {count} ServiceRequests in {sw.Elapsed.TotalSeconds:F1}s");
        await LoadIdMapsAsync();
    }

    public async Task ImportDocumentsAsync(HashSet<int> parentIds, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var docBulk = new List<DocumentNode>();
        var importDataBulk = new List<DocumentImport>();
        var importCount = 0;
        var level = 1;

        var currentParentIds = parentIds;

        while (currentParentIds.Any())
        {
            Console.WriteLine($"--- Level {level}: importing children of {currentParentIds.Count} parents ---");
            var children = await oldDb.GetChildObjectsAsync(currentParentIds, SyncExtensions.NewDocIds.Keys.ToHashSet(), ct);
            Console.WriteLine($"Found {children.Count} child objects");

            var nextParentIds = new HashSet<int>();
            foreach (var o in children)
            {
                if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

                var n = o.ToDocument(AdminUserId);
                docBulk.Add(n);

                var dataStr = SyncExtensions.DecodeData(o.Data);
                var infoStr = SyncExtensions.DecodeData(o.Info);
                importDataBulk.Add(new DocumentImport
                {
                    Id = Guid.CreateVersion7(),
                    DocumentId = n.Id,
                    Info = infoStr,
                    Data = dataStr
                });

                nextParentIds.Add(o.Id);
                importCount++;

                if (docBulk.Count >= BulkSize)
                {
                    await BulkSaveDocumentsAsync(docBulk, importDataBulk, ct);
                    docBulk.Clear();
                    importDataBulk.Clear();
                }
            }

            if (docBulk.Any())
                await BulkSaveDocumentsAsync(docBulk, importDataBulk, ct);

            docBulk.Clear();
            importDataBulk.Clear();
            currentParentIds = nextParentIds;
            level++;
        }

        Console.WriteLine($"Imported {importCount} documents in {sw.Elapsed.TotalSeconds:F1}s");
        await LoadIdMapsAsync();
    }

    private async Task BulkSaveAsync(
        List<ServiceRequest> requests,
        List<ServiceRequestImport> importData,
        List<Annotation> annotations,
        CancellationToken ct)
    {
        using var tx = await newDb.Database.BeginTransactionAsync(ct);
        try
        {
            await newDb.BulkInsertAsync(requests, cancellationToken: ct);
            if (importData.Any())
                await newDb.BulkInsertAsync(importData, cancellationToken: ct);
            if (annotations.Any())
                await newDb.BulkInsertAsync(annotations, cancellationToken: ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Bulk save error: {ex.Message}");
            await tx.RollbackAsync(ct);
            throw;
        }
        newDb.ChangeTracker.Clear();
    }

    private async Task BulkSaveDocumentsAsync(
        List<DocumentNode> documents,
        List<DocumentImport> importData,
        CancellationToken ct)
    {
        using var tx = await newDb.Database.BeginTransactionAsync(ct);
        try
        {
            await newDb.BulkInsertAsync(documents, cancellationToken: ct);
            if (importData.Any())
                await newDb.BulkInsertAsync(importData, cancellationToken: ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Bulk document save error: {ex.Message}");
            await tx.RollbackAsync(ct);
            throw;
        }
        newDb.ChangeTracker.Clear();
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add import/DataImport/SyncService.cs
git commit -m "feat: add SyncService orchestrator for live import"
```

---

### Task 5: Wire up `Program.cs` entry point

**Files:**
- Modify: `import/DataImport/Program.cs`

Add a `sync` argument path that launches the sync service instead of the bulk import.

- [ ] **Step 1: Modify Program.cs**

Add a `sync` path alongside the existing `icdo` check. Insert after the `icdo` block and before the else block:

```csharp
using iPath.DataImport;
using iPath.API;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DispatchR.Extensions;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

if (System.IO.File.Exists("importsettings.json"))
{
    builder.Configuration.AddJsonFile("importsettings.json");
}
else
{
    throw new Exception("No configuration 'importsetting.json' found");
}

// Also try loading syncsettings.json (optional — falls back to importsettings.json)
if (System.IO.File.Exists("syncsettings.json"))
{
    builder.Configuration.AddJsonFile("syncsettings.json");
}

builder.Services.AddDispatchR(cfg => cfg.Assemblies.Add(typeof(iPath.Application.Meta).Assembly));
builder.Services.AddPersistance(builder.Configuration);
builder.Services.AddIPathAuthentication(builder.Configuration);

var cs = builder.Configuration.GetConnectionString("ipath_old");
Console.WriteLine("Data Source: " + cs);
builder.Services.AddDbContext<OldDB>(cfg => cfg.UseMySQL(cs));

builder.Services.Configure<ImportConfig>(options => builder.Configuration.GetSection(nameof(ImportConfig)).Bind(options));
builder.Services.AddScoped<ImportService>();

// --- Sync service registration ---
builder.Services.Configure<SyncConfig>(options => builder.Configuration.GetSection(nameof(SyncConfig)).Bind(options));
builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SyncConfig>>().Value;
    return new OldDataService(cfg.OldConnectionString);
});
builder.Services.AddScoped<SyncService>();
// ---

using IHost host = builder.Build();

if (args.Contains("icdo"))
{
    CodingImport.ImportCodes();
}
else if (args.Contains("sync"))
{
    // Live sync — no DB preparation needed
    var sync = host.Services.GetRequiredService<SyncService>();
    var cfg = host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<SyncConfig>>().Value;

    await sync.InitAsync();

    if (cfg.ImportUsers)
        await sync.ImportUsersAsync();

    if (cfg.ImportCommunities)
        await sync.ImportCommunitiesAsync();

    if (cfg.ImportGroups)
        await sync.ImportGroupsAsync();

    if (cfg.ImportServiceRequests)
    {
        var groupIds = (await host.Services.GetRequiredService<iPathDbContext>()
            .Groups.Where(g => g.ipath2_id.HasValue)
            .Select(g => g.ipath2_id!.Value)
            .ToListAsync()).ToHashSet();
        await sync.ImportServiceRequestsAsync(groupIds);
    }

    if (cfg.ImportDocuments)
    {
        var rootIds = SyncExtensions.NewNodeIds.Values.Select(_ => SyncExtensions.NewNodeIds.First().Key).ToHashSet();
        // Actually, we need the old IDs of imported roots:
        var oldRootIds = SyncExtensions.NewNodeIds.Keys.ToHashSet();
        await sync.ImportDocumentsAsync(oldRootIds);
    }

    Console.WriteLine("Sync complete.");
}
else
{
    await DatabaseImport.StartImport(host.Services);
}
```

- [ ] **Step 2: Commit**

```bash
git add import/DataImport/Program.cs
git commit -m "feat: wire sync entry point in Program.cs"
```

---

### Task 6: Verify the sync path at least compiles

**Files:**
- Run: `import/DataImport/`

- [ ] **Step 1: Build the project**

```bash
dotnet build import/DataImport/iPath.DataImport.csproj
```

Expected: Build succeeds with no errors. The sync path is behind a `sync` argument gate so existing `dotnet run` behavior is unchanged.

- [ ] **Step 2: Commit any build fixes**

```bash
git commit -am "fix: resolve build issues in sync service"
```

---

### Summary

| Concern | Old import (untouched) | New sync service |
|---|---|---|
| Reading old DB | EF Core OldDB : DbContext | Dapper + raw SQL via OldDataService |
| Encoding | Relies on prep SQL `CONVERT(BINARY CONVERT(...))` | `CAST(col AS BINARY)` + `Encoding.UTF8.GetString()` in C# |
| `_top_id` parent chain | Prep SQL computes it, EF Core navigates via it | Level-by-level queries — no `_top_id` needed |
| `community_group` PK | Prep SQL adds auto-increment PK | Raw SELECT — no PK needed |
| Orphaned FK refs | Prep SQL deletes them | Handled in code (skip missing refs) |
| Trigger | Console app | Blazor admin page + REST API |
| Old DB changes required | Yes (prep SQL) | None |

### What's NOT included (v1 scope):
- User/community/group sync (can be added later — same pattern)
- Visit stats import
- Annotation import during node sync
- Two-way sync or conflict resolution
