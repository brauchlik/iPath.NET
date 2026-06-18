using iPath.Application.Features.SyncImport;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Xml;

namespace iPath.API.Services.SyncImport;

public class SyncImportRunner(
    OldDataService oldDb,
    iPathDbContext newDb,
    UserManager<User> um,
    RoleManager<Role> rm,
    ILogger<SyncImportRunner> logger) : ISyncImportRunner
{
    private readonly Dictionary<int, Guid> _userIds = [];
    private readonly Dictionary<int, Guid> _groupIds = [];
    private readonly Dictionary<int, Guid> _communityIds = [];
    private readonly Dictionary<int, Guid> _nodeIds = [];
    private readonly Dictionary<int, Guid> _docIds = [];
    private readonly Dictionary<int, Guid> _docRootIds = []; // old doc id → root ServiceRequest GUID
    private readonly Dictionary<int, Guid> _annotationIds = [];
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
        _adminUserId = (await newDb.Users.FirstOrDefaultAsync(u => u.NormalizedUserName == "ADMIN"))?.Id
                       ?? (await newDb.Users.FirstOrDefaultAsync())?.Id
                       ?? throw new InvalidOperationException("No users exist in the new database. Create at least one admin user first.");
        _defaultCommunityId = (await newDb.Communities.FirstOrDefaultAsync())?.Id ?? Guid.Empty;
    }

    private async Task LoadIdMapsAsync()
    {
        _userIds.Clear(); _groupIds.Clear(); _communityIds.Clear(); _nodeIds.Clear(); _docIds.Clear(); _annotationIds.Clear();

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
        foreach (var a in await newDb.Annotations.Where(a => a.ipath2_id.HasValue).ToListAsync())
            _annotationIds[a.ipath2_id!.Value] = a.Id;

        logger.LogInformation("Loaded {Users} users, {Groups} groups, {Communities} communities, {Nodes} nodes, {Docs} docs, {Annotations} annotations",
            _userIds.Count, _groupIds.Count, _communityIds.Count, _nodeIds.Count, _docIds.Count, _annotationIds.Count);
    }

    private Guid? MapUserId(int? id) => id.HasValue && _userIds.TryGetValue(id.Value, out var g) ? g : null;
    private Guid? MapGroupId(int? id) => id.HasValue && _groupIds.TryGetValue(id.Value, out var g) ? g : null;
    private Guid? MapCommunityId(int? id) => id.HasValue && _communityIds.TryGetValue(id.Value, out var g) ? g : null;
    private Guid MapNodeId(int id) => _nodeIds.TryGetValue(id, out var g) ? g : (_nodeIds[id] = Guid.CreateVersion7());
    private Guid MapDocId(int id) => _docIds.TryGetValue(id, out var g) ? g : (_docIds[id] = Guid.CreateVersion7());

    private static string? Decode(byte[]? raw) => raw is { Length: > 0 } ? Encoding.UTF8.GetString(raw) : null;

    private async Task<int> SaveWithDiagnosticsAsync(CancellationToken ct, [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        try
        {
            return await newDb.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            foreach (var entry in ex.Entries)
            {
                logger.LogError("CONCURRENCY ERROR in {Caller}: EntityType={Entity}, State={State}, Id={Id}",
                    caller,
                    entry.Entity.GetType().Name,
                    entry.State,
                    entry.Property("Id")?.CurrentValue);
            }
            throw;
        }
    }

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

    public async Task<int> ImportUsersAsync(CancellationToken ct = default)
    {
        await LoadIdMapsAsync();
        var oldPersons = await oldDb.GetPersonsAsync(ct);
        var count = 0;
        const int batchSize = 500;
        var roleAdmin = await rm.FindByNameAsync("Admin");
        var roleTranslator = await rm.FindByNameAsync("Translator");
        var usedUsernames = new HashSet<string>(
            await newDb.Users.Select(u => u.NormalizedUserName).ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);

        foreach (var p in oldPersons)
        {
            if (_userIds.ContainsKey(p.Id)) continue;

            User u;
            try { u = CreateUserEntity(p, usedUsernames, roleAdmin, roleTranslator); }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning("Skipped user {Username} (id={Id}): {Reason}", p.Username, p.Id, ex.Message);
                continue;
            }

            newDb.Users.Add(u);
            _userIds[p.Id] = u.Id;
            count++;

            if (count % batchSize == 0)
            {
                await newDb.SaveChangesAsync(ct);
                logger.LogInformation("Imported {Count} users...", count);
            }
        }

        await newDb.SaveChangesAsync(ct);
        logger.LogInformation("ImportUsersAsync: imported {Count} new users", count);
        return count;
    }

    private static string? SanitizeUsername(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        // Remove characters ASP.NET Identity rejects: /, \, space, etc.
        var sanitized = raw.Trim();
        if (sanitized.Any(c => !char.IsLetterOrDigit(c) && c != '_' && c != '-' && c != '.'))
        {
            // Replace non-allowed chars with underscore
            var sb = new StringBuilder(sanitized.Length);
            foreach (var c in sanitized)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.')
                    sb.Append(c);
                else
                    sb.Append('_');
            }
            sanitized = sb.ToString();
        }
        return sanitized.Length > 0 ? sanitized : null;
    }

    private User CreateUserEntity(OldPersonDto p, HashSet<string> usedUsernames, Role? roleAdmin, Role? roleTranslator)
    {
        var username = SanitizeUsername(p.Username);
        if (username is null)
            throw new InvalidOperationException($"Cannot create user: invalid username '{p.Username}'");

        while (!usedUsernames.Add(username))
            username = $"{p.Id}_{username}";

        var u = new User
        {
            Id = Guid.CreateVersion7(),
            ipath2_id = p.Id,
            ipath2_username = p.Username,
            ipath2_password = p.Password,
            UserName = username,
            NormalizedUserName = username.ToUpperInvariant(),
            Email = string.IsNullOrWhiteSpace(p.Email) ? $"{username}@import.local" : p.Email.Trim(),
            NormalizedEmail = (string.IsNullOrWhiteSpace(p.Email) ? $"{username}@import.local" : p.Email.Trim()).ToUpperInvariant(),
            PasswordHash = "-",
            CreatedOn = p.Entered?.ToUniversalTime() ?? DateTime.UtcNow,
            IsActive = (p.Status & 2) == 0,
            SecurityStamp = Guid.CreateVersion7().ToString(),
            ConcurrencyStamp = Guid.CreateVersion7().ToString(),
        };

        var xml = LoadXml(p.Data);
        u.Profile.UserId = u.Id;
        u.Profile.Username = username;
        u.Profile.FirstName = xml.SelectSingleNode("/data/firstname")?.InnerText;
        u.Profile.FamilyName = xml.SelectSingleNode("/data/name")?.InnerText;
        u.Profile.Specialisation = xml.SelectSingleNode("/data/specialisation")?.InnerText;
        if (!string.IsNullOrEmpty(u.Profile.FirstName))
        {
            u.Profile.Initials = u.Profile.FirstName[..1];
            u.Profile.Initials += string.IsNullOrEmpty(u.Profile.FamilyName) ? "" : u.Profile.FamilyName[..1];
        }
        else
        {
            u.Profile.Initials = username.Length > 0 ? username[..1] : "?";
        }
        u.Profile.EmailAddress = u.Email;
        u.Profile.ContactDetails.Organisation = xml.SelectSingleNode("/data/institute")?.InnerText;
        u.Profile.ContactDetails.PhoneNr = xml.SelectSingleNode("/data/phone")?.InnerText;
        u.Profile.ContactDetails.Email = u.Email;
        u.Profile.ContactDetails.Address.Street = xml.SelectSingleNode("/data/street")?.InnerText;
        u.Profile.ContactDetails.Address.PostalCode = xml.SelectSingleNode("/data/zip")?.InnerText;
        u.Profile.ContactDetails.Address.City = xml.SelectSingleNode("/data/city")?.InnerText;
        u.Profile.ContactDetails.Address.Country = xml.SelectSingleNode("/data/country")?.InnerText;

        if (p.Id == 1 && roleAdmin is not null)
            u.Roles.Add(roleAdmin);
        if ((p.Status & 4) != 0 && roleTranslator is not null)
            u.Roles.Add(roleTranslator);

        return u;
    }

    public async Task<int> SyncCommunitiesAndGroupsAsync(CancellationToken ct = default)
    {
        await LoadIdMapsAsync();
        _adminUserId = (await newDb.Users.FirstOrDefaultAsync(u => u.NormalizedUserName == "ADMIN", ct))?.Id
                       ?? (await newDb.Users.FirstOrDefaultAsync(ct))?.Id
                       ?? throw new InvalidOperationException("No users exist");
        _defaultCommunityId = (await newDb.Communities.FirstOrDefaultAsync(ct))?.Id ?? Guid.Empty;
        var count = 0;

        // Import communities
        var oldCommunities = await oldDb.GetCommunitiesAsync(ct);
        var groupToComm = (await oldDb.GetCommunityGroupsAsync(ct))
            .GroupBy(cg => cg.Group_id)
            .ToDictionary(g => g.Key, g => g.First().Community_id);

        foreach (var oc in oldCommunities)
        {
            if (_communityIds.ContainsKey(oc.Id)) continue;
            var nc = Community.Create(oc.Name, _adminUserId);
            nc.CreatedOn = oc.Created_on.ToUniversalTime();
            nc.ipath2_id = oc.Id;
            newDb.Communities.Add(nc);
            _communityIds[oc.Id] = nc.Id;
            count++;
        }

        // Import groups
        var oldGroups = await oldDb.GetGroupsAsync(ct);
        foreach (var og in oldGroups)
        {
            if (_groupIds.ContainsKey(og.Id)) continue;
            var communityId = groupToComm.TryGetValue(og.Id, out var cid) ? cid : (int?)null;
            var ng = Group.Create(og.Name, _adminUserId, MapCommunityId(communityId));
            ng.ipath2_id = og.Id;
            newDb.Groups.Add(ng);
            _groupIds[og.Id] = ng.Id;
            count++;
        }

        await newDb.SaveChangesAsync(ct);

        await ImportGroupMembersAsync(ct);

        return count;
    }

    private async Task ImportGroupMembersAsync(CancellationToken ct)
    {
        var oldMembers = await oldDb.GetGroupMembersAsync(ct);
        var grouped = oldMembers.GroupBy(m => m.Group_id).ToDictionary(g => g.Key);
        var memberCount = 0;

        foreach (var (oldGroupId, members) in grouped)
        {
            if (!_groupIds.TryGetValue(oldGroupId, out var newGroupId)) continue;
            var grp = await newDb.Groups.FindAsync([newGroupId], ct);
            if (grp is null) continue;

            foreach (var m in members)
            {
                if (!_userIds.TryGetValue(m.User_id, out var uid)) continue;
                var role = eMemberRole.User;
                if ((m.Status & 4) != 0) role = eMemberRole.Moderator;
                if ((m.Status & 2) != 0) role = eMemberRole.Banned;
                if ((m.Status & 8) != 0) role = eMemberRole.Guest;
                grp.AddMember(uid, role);
                memberCount++;
            }
        }

        await newDb.SaveChangesAsync(ct);
        logger.LogInformation("Imported {Count} group members", memberCount);
    }

    public async Task<int> SyncGroupAsync(int groupId, CancellationToken ct = default,
        IProgress<(int Current, int Total, string Status)>? progress = null,
        int progressOffset = 0, int totalWork = 0)
    {
        // Estimate total work early so we can fire a progress report before LoadIdMapsAsync
        if (totalWork == 0)
        {
            var rootCount = await oldDb.CountRootObjectsAsync(groupId, ct);
            var childDocCount = await oldDb.CountChildObjectsForGroupAsync(groupId, ct);
            var annotationCount = await oldDb.CountAnnotationsForGroupAsync(groupId, ct);
            totalWork = rootCount + childDocCount + annotationCount;
        }
        var completed = progressOffset;
        progress?.Report((completed, totalWork, "Mapping IDs..."));

        await LoadIdMapsAsync();

        if (!_groupIds.ContainsKey(groupId))
        {
            await ImportGroupWithMembersAsync(groupId, ct);
        }

        // Calculate max annotation ipath2_id already imported for this group
        var groupGuid = _groupIds[groupId];
        var maxAnnotationId = await (
            from a in newDb.Annotations
            join sr in newDb.ServiceRequests on a.ServiceRequestId equals sr.Id
            where a.ipath2_id.HasValue && sr.GroupId!.Equals(groupGuid)
            select a.ipath2_id!.Value
        ).DefaultIfEmpty().MaxAsync(ct);

        var roots = await oldDb.GetRootObjectsAsync([groupId], ct);
        var rootsToImport = roots.Where(r => !_nodeIds.ContainsKey(r.Id)).ToList();
        logger.LogInformation("Syncing group {GroupId}: {Count} new root objects", groupId, rootsToImport.Count);

        // Import root objects
        foreach (var o in rootsToImport)
        {
            if (ct.IsCancellationRequested) break;
            await ImportServiceRequestAsync(o, ct);
            completed++;
            progress?.Report((completed, totalWork, "Importing root objects..."));
        }
        await SaveWithDiagnosticsAsync(ct);

        // Import annotations for root objects
        if (rootsToImport.Any())
        {
            progress?.Report((completed, totalWork, "Importing annotations..."));
            completed = await ImportAnnotationsForObjectsAsync(rootsToImport.Select(r => r.Id).ToHashSet(), maxAnnotationId, ct, progress, completed, totalWork);
        }

        // Import child documents and their annotations
        var parentIds = rootsToImport.Select(r => r.Id).ToHashSet();
        while (parentIds.Any())
        {
            var children = await oldDb.GetChildObjectsAsync(parentIds, ct);
            var newChildren = children.Where(c => !_docIds.ContainsKey(c.Id)).ToList();
            progress?.Report((completed, totalWork, "Importing child documents..."));
            foreach (var o in newChildren)
                await ImportDocumentAsync(o, ct);
            await SaveWithDiagnosticsAsync(ct);

            if (newChildren.Any())
            {
                progress?.Report((completed, totalWork, "Importing annotations..."));
                completed = await ImportAnnotationsForObjectsAsync(newChildren.Select(c => c.Id).ToHashSet(), maxAnnotationId, ct, progress, completed, totalWork);
            }

            parentIds = newChildren.Select(c => c.Id).ToHashSet();
        }

        await LoadIdMapsAsync();
        return rootsToImport.Count;
    }

    private async Task ImportGroupWithMembersAsync(int groupId, CancellationToken ct)
    {
        var oldGroup = await oldDb.GetGroupAsync(groupId, ct);
        if (oldGroup is null)
            throw new InvalidOperationException($"Group {groupId} not found in old database");

        var ng = Group.Create(oldGroup.Name, _adminUserId, null);
        ng.ipath2_id = oldGroup.Id;
        newDb.Groups.Add(ng);
        _groupIds[groupId] = ng.Id;

        var members = await oldDb.GetGroupMembersForGroupAsync(groupId, ct);
        var roleAdmin = await rm.FindByNameAsync("Admin");
        var roleTranslator = await rm.FindByNameAsync("Translator");
        var usedUsernames = new HashSet<string>(
            await newDb.Users.Select(u => u.NormalizedUserName).ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);

        foreach (var m in members)
        {
            if (_userIds.ContainsKey(m.User_id)) continue;
            var p = await oldDb.GetPersonAsync(m.User_id, ct);
            if (p is null) continue;

            User u;
            try { u = CreateUserEntity(p, usedUsernames, roleAdmin, roleTranslator); }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning("Skipped user {Username} (id={Id}): {Reason}", p.Username, p.Id, ex.Message);
                continue;
            }

            newDb.Users.Add(u);
            _userIds[p.Id] = u.Id;
        }

        var grp = await newDb.Groups.FindAsync([ng.Id], ct);
        if (grp is not null)
        {
            foreach (var m in members)
            {
                if (!_userIds.TryGetValue(m.User_id, out var uid)) continue;
                var role = eMemberRole.User;
                if ((m.Status & 4) != 0) role = eMemberRole.Moderator;
                if ((m.Status & 2) != 0) role = eMemberRole.Banned;
                if ((m.Status & 8) != 0) role = eMemberRole.Guest;
                grp.AddMember(uid, role);
            }
        }

        await SaveWithDiagnosticsAsync(ct);
        logger.LogInformation("Imported group {Name} (id={Id}) with {Members} members",
            oldGroup.Name, groupId, members.Count);
    }

    private async Task<int> ImportAnnotationsForObjectsAsync(HashSet<int> oldObjectIds, int minId, CancellationToken ct,
        IProgress<(int Current, int Total, string Status)>? progress = null, int completed = 0, int totalWork = 0)
    {
        var oldAnnotations = await oldDb.GetAnnotationsForObjectsAsync(oldObjectIds, minId, ct);
        if (!oldAnnotations.Any()) return completed;

        var count = 0;
        var lastReported = 0;
        const int batchSize = 500;

        foreach (var a in oldAnnotations)
        {
            if (_annotationIds.ContainsKey(a.Id)) continue;
            if (!_userIds.TryGetValue(a.Sender_id, out var uid)) continue;

            var n = new Annotation
            {
                Id = Guid.CreateVersion7(),
                ipath2_id = a.Id,
                CreatedOn = a.Entered.ToUniversalTime(),
                OwnerId = uid,
            };

            if (_nodeIds.TryGetValue(a.Object_id, out var srId))
            {
                n.ServiceRequestId = srId;
            }
            else if (_docIds.TryGetValue(a.Object_id, out var docId))
            {
                n.DcoumentNodeId = docId;
                n.ServiceRequestId = _docRootIds.TryGetValue(a.Object_id, out var rootId) ? rootId : null;
            }
            else
            {
                continue;
            }

            n.Data = new AnnotationData { Type = eAnnotationType.Comment };
            var xml = LoadXml(a.Data);
            n.Data.Text = xml.SelectSingleNode("/data/text")?.InnerText;

            newDb.Set<Annotation>().Add(n);
            _annotationIds[a.Id] = n.Id;
            count++;

            if (count % batchSize == 0)
            {
                await SaveWithDiagnosticsAsync(ct);
                completed += count - lastReported;
                lastReported = count;
                progress?.Report((completed, totalWork, "Importing annotations..."));
            }
        }

        await SaveWithDiagnosticsAsync(ct);
        completed += count - lastReported;
        progress?.Report((completed, totalWork, "Importing annotations..."));
        logger.LogInformation("Imported {Count} annotations", count);
        return completed;
    }

    async Task<SyncStartResponse> ISyncImportRunner.SyncGroupAsync(SyncStartRequest request, CancellationToken ct)
    {
        await InitAsync();
        var count = await SyncGroupAsync(request.GroupId, ct);
        return new SyncStartResponse($"Synced {count} root nodes from group {request.GroupId}");
    }

    async Task<SyncStartResponse> ISyncImportRunner.SyncGroupWithProgressAsync(int groupId, IProgress<(int Current, int Total, string Status)> progress, CancellationToken ct)
    {
        await InitAsync();
        var count = await SyncGroupAsync(groupId, ct, progress);
        return new SyncStartResponse($"Synced {count} root nodes from group {groupId}");
    }

    async Task<GroupImportResult> ISyncImportRunner.ReimportGroupAsync(int groupId, IProgress<(int Current, int Total, string Status)>? progress, CancellationToken ct)
    {
        await InitAsync();

        if (!_groupIds.TryGetValue(groupId, out var groupGuid))
            return new GroupImportResult(0, $"Group {groupId} not found in new database", false);

        // Estimate total work from old database for progress reporting
        var rootCount = await oldDb.CountRootObjectsAsync(groupId, ct);
        var childDocCount = await oldDb.CountChildObjectsForGroupAsync(groupId, ct);
        var annotationCount = await oldDb.CountAnnotationsForGroupAsync(groupId, ct);
        var totalWork = rootCount + childDocCount + annotationCount + 2; // +2 for delete phase + members phase
        var completed = 0;
        progress?.Report((completed, totalWork, "Deleting existing data..."));

        logger.LogInformation("Re-importing group {GroupId}: deleting existing synced data...", groupId);

        // Get all imported SR IDs for this group
        var srIds = await newDb.ServiceRequests
            .Where(sr => sr.GroupId == groupGuid && sr.ipath2_id.HasValue)
            .Select(sr => sr.Id)
            .ToListAsync(ct);

        if (srIds.Count > 0)
        {
            // Get all imported document IDs belonging to these SRs
            var docIds = await newDb.Documents
                .Where(d => d.ipath2_id.HasValue && srIds.Contains(d.ServiceRequestId))
                .Select(d => d.Id)
                .ToListAsync(ct);

            // Delete in FK-safe order: annotations → lastvisits → docimports → docs → srimports → srs
            await newDb.Annotations
                .Where(a => (a.ServiceRequestId.HasValue && srIds.Contains(a.ServiceRequestId.Value)) ||
                            (a.DcoumentNodeId.HasValue && docIds.Contains(a.DcoumentNodeId.Value)))
                .ExecuteDeleteAsync(ct);

            await newDb.Set<ServiceRequestLastVisit>()
                .Where(lv => srIds.Contains(lv.ServiceRequestId))
                .ExecuteDeleteAsync(ct);

            await newDb.Set<DocumentImport>()
                .Where(di => docIds.Contains(di.DocumentId))
                .ExecuteDeleteAsync(ct);

            await newDb.Documents.Where(d => docIds.Contains(d.Id)).ExecuteDeleteAsync(ct);

            await newDb.Set<ServiceRequestImport>()
                .Where(sri => srIds.Contains(sri.ServiceRequestId))
                .ExecuteDeleteAsync(ct);

            await newDb.ServiceRequests.Where(sr => srIds.Contains(sr.Id)).ExecuteDeleteAsync(ct);
        }

        // Delete group members
        await newDb.Set<GroupMember>()
            .Where(gm => gm.GroupId == groupGuid)
            .ExecuteDeleteAsync(ct);

        // Clear change tracker to remove stale entities loaded by LoadIdMapsAsync
        // whose rows were just deleted by ExecuteDeleteAsync
        newDb.ChangeTracker.Clear();

        // Delete the group itself
        var grp = await newDb.Groups.FindAsync([groupGuid], ct);
        if (grp is not null)
            newDb.Groups.Remove(grp);

        await SaveWithDiagnosticsAsync(ct);
        completed = 1;
        progress?.Report((completed, totalWork, "Importing missing users..."));
        logger.LogInformation("Re-importing group {GroupId}: old data deleted", groupId);

        // Import missing users for this group's members
        logger.LogInformation("Re-importing group {GroupId}: importing missing users...", groupId);
        var oldMembers = await oldDb.GetGroupMembersForGroupAsync(groupId, ct);
        var roleAdmin = await rm.FindByNameAsync("Admin");
        var roleTranslator = await rm.FindByNameAsync("Translator");
        var usedUsernames = new HashSet<string>(
            await newDb.Users.Select(u => u.NormalizedUserName).ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);

        foreach (var m in oldMembers)
        {
            if (_userIds.ContainsKey(m.User_id)) continue;
            var p = await oldDb.GetPersonAsync(m.User_id, ct);
            if (p is null) continue;
            User u;
            try { u = CreateUserEntity(p, usedUsernames, roleAdmin, roleTranslator); }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning("Skipped user {Username} (id={Id}): {Reason}", p.Username, p.Id, ex.Message);
                continue;
            }
            newDb.Users.Add(u);
            _userIds[p.Id] = u.Id;
        }
        await SaveWithDiagnosticsAsync(ct);

        // Reimport group members into the existing group
        grp = await newDb.Groups.FindAsync([groupGuid], ct);
        if (grp is not null)
        {
            foreach (var m in oldMembers)
            {
                if (!_userIds.TryGetValue(m.User_id, out var uid)) continue;
                var role = eMemberRole.User;
                if ((m.Status & 4) != 0) role = eMemberRole.Moderator;
                if ((m.Status & 2) != 0) role = eMemberRole.Banned;
                if ((m.Status & 8) != 0) role = eMemberRole.Guest;
                grp.AddMember(uid, role);
            }
            await SaveWithDiagnosticsAsync(ct);
        }

        completed = 2;
        progress?.Report((completed, totalWork, "Importing root objects..."));
        logger.LogInformation("Re-importing group {GroupId}: members re-imported, now importing nodes...", groupId);

        // Reimport root objects, child documents, and annotations with full progress tracking
        var count = await SyncGroupAsync(groupId, ct, progress, completed, totalWork);
        return new GroupImportResult(count, $"Re-imported {count} root nodes from group {groupId}", true);
    }

    async Task<int> ISyncImportRunner.ImportUsersAsync(CancellationToken ct)
    {
        return await ImportUsersAsync(ct);
    }

    async Task<SyncStartResponse> ISyncImportRunner.SyncGroupsAsync(int[] groupIds, CancellationToken ct)
    {
        await InitAsync();
        var totalRoots = 0;
        foreach (var gid in groupIds)
        {
            var n = await SyncGroupAsync(gid, ct);
            totalRoots += n;
            logger.LogInformation("Group {GroupId}: {Count} roots synced", gid, n);
        }
        return new SyncStartResponse($"Synced {totalRoots} root nodes from {groupIds.Length} groups");
    }

    async Task<int> ISyncImportRunner.ImportLastVisitsAsync(int[] groupIds, CancellationToken ct)
    {
        await InitAsync();
        await newDb.Set<ServiceRequestLastVisit>().ExecuteDeleteAsync(ct);

        var lastVisits = await oldDb.GetLastVisitsForGroupsAsync([.. groupIds], ct);
        var batch = new List<ServiceRequestLastVisit>(lastVisits.Count);

        foreach (var lv in lastVisits)
        {
            if (!_userIds.TryGetValue(lv.User_id, out var uid)) continue;
            if (!_nodeIds.TryGetValue(lv.Object_id, out var nid)) continue;
            batch.Add(ServiceRequestLastVisit.Create(uid, nid, lv.Visitdate.ToUniversalTime()));
        }

        if (batch.Count > 0)
        {
        const int batchSize = 50;
            for (int i = 0; i < batch.Count; i += batchSize)
            {
                var chunk = batch.Skip(i).Take(batchSize).ToList();
                newDb.Set<ServiceRequestLastVisit>().AddRange(chunk);
                await newDb.SaveChangesAsync(ct);
            }
        }

        logger.LogInformation("ImportLastVisitsAsync: imported {Count} of {Total} records", batch.Count, lastVisits.Count);
        return batch.Count;
    }

    async Task<GroupImportStatus> ISyncImportRunner.GetGroupImportStatusAsync(int groupId, CancellationToken ct)
    {
        var oldGroup = await oldDb.GetGroupAsync(groupId, ct);
        var oldRootCount = await oldDb.CountRootObjectsAsync(groupId, ct);
        var newGroup = await newDb.Groups.FirstOrDefaultAsync(g => g.ipath2_id == groupId, ct);
        var syncedRootCount = newGroup is null
            ? 0
            : await newDb.ServiceRequests.Where(sr => sr.GroupId == newGroup.Id && sr.ipath2_id.HasValue).CountAsync(ct);
        var annotationCount = await oldDb.CountAnnotationsForGroupAsync(groupId, ct);
        var members = await oldDb.GetGroupMembersForGroupAsync(groupId, ct);

        return new GroupImportStatus
        {
            GroupName = oldGroup?.Name ?? $"Group #{groupId}",
            OldRootCount = oldRootCount,
            SyncedRootCount = syncedRootCount,
            AnnotationCount = annotationCount,
            UserCount = members.Count
        };
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

        // Resolve ServiceRequestId (root node) via parent chain
        if (o.Parent_id.HasValue)
        {
            if (_nodeIds.ContainsKey(o.Parent_id.Value))
            {
                // Parent is a root (ServiceRequest)
                n.ServiceRequestId = _nodeIds[o.Parent_id.Value];
                // ParentNodeId stays null — level-1 doc has no document parent
            }
            else if (_docRootIds.TryGetValue(o.Parent_id.Value, out var rootId))
            {
                // Parent is another document — use its root
                n.ServiceRequestId = rootId;
                n.ParentNodeId = _docIds[o.Parent_id.Value];
            }
            else
            {
                throw new InvalidOperationException($"Document {o.Id}: parent {o.Parent_id} not found as root or document");
            }
        }
        else
        {
            throw new InvalidOperationException($"Document {o.Id} has no parent");
        }
        _docRootIds[o.Id] = n.ServiceRequestId;

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
