using Cyrillic.Convert;
using EFCore.BulkExtensions;
using Humanizer;
using iPath.EF.Core.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text;

namespace iPath.DataImport;

public class ImportService(OldDB oldDb, iPathDbContext newDb,
    UserManager<User> um, RoleManager<Role> rm)
{
    public int BulkSize { get; set; } = 10_000;

    void OnMessage(string msg)
    {
        Console.WriteLine(msg);
    }
    void OnProgress(int progress, string msg)
    {
        Console.WriteLine(msg);
    }


    public async Task MigrateDatbase()
    {
        await newDb.Database.MigrateAsync();
    }

    public async Task TestInsert()
    {
        var c = new Node()
        {
            Id = Guid.CreateVersion7(),
            NodeType = "test"
        };

        c.Owner = await newDb.Users.FirstOrDefaultAsync();
        c.Group = await newDb.Groups.FirstOrDefaultAsync();

        c.Description = new();
        c.Description.Title = "Test Case";

        var id = c.Id.ToString();
        await newDb.Nodes.AddAsync(c);

        if (id != c.Id.ToString())
        {
            Console.WriteLine("UUID has changed on AddAsync {0} => {1}", id, c.Id);
        }
        else
        {
            await newDb.SaveChangesAsync();
            Console.WriteLine("node #{0} saved", id);

            newDb.Nodes.Remove(c);
            await newDb.SaveChangesAsync();
            Console.WriteLine("node #{0} deleted", id);
        }
    }


    public async Task<string> GetUserNameChars()
    {
        var sb = new StringBuilder();
        sb.Append("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+");
        var conv = new Conversion();

        await foreach(var username in oldDb.persons.Select(p => p.username).ToAsyncEnumerable())
        {
            var u = DataImportExtensions.CleanUsername(username);
            sb.Append(u);
        }

        var chars = sb.ToString().Distinct().ToList();
        chars.Sort();
        var newstr = String.Join("", chars); //  sb.ToString().Distinct());
        return newstr;
    }


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

        oldDb.Database.SetCommandTimeout(3600);
        newDb.Database.SetCommandTimeout(3600);

        await DataImportExtensions.InitIdDictAsync(newDb);

        var oldUsers = await oldDb.persons.Select(p => new { Id = p.id, Username = p.username, Email = p.email }).ToListAsync();
        var missingUsers = new List<int>();
        foreach (var u in oldUsers)
        {
            if (!DataImportExtensions.userIds.ContainsKey(u.Id))
            {
                missingUsers.Add(u.Id);
                Console.WriteLine($"Missing User #{u.Id} - User {u.Username}, {u.Email} has not been migrated");
            }
        }

        Console.WriteLine("existing users: " + DataImportExtensions.userIds.Count());
    }



    public async Task ImportUsersAsync(CancellationToken ct = default)
    {
        OnMessage("getting old users ... ");
        var userCount = await oldDb.persons.CountAsync(u => !DataImportExtensions.userIds.Keys.Contains(u.id));
        OnMessage($"reading {userCount} Users ... ");
        var users = oldDb.persons
            .Where(u => !DataImportExtensions.userIds.Keys.Contains(u.id))
            .AsNoTracking()
            .AsAsyncEnumerable();

        var oldCount = await users.CountAsync();

        // clean cache
        DataImportExtensions.usernames = new();

        var bulk = new List<User>();
        var c = 0;
        await foreach (var u in users)
        {
            if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

            if (!DataImportExtensions.userIds.ContainsKey(u.id))
            {
                try
                {
                    var newUser = u.ToNewEntity();
                    var res = await um.CreateAsync(newUser);
                    if (!res.Succeeded)
                        throw new Exception();
                                       

                    // User 1 = Admin
                    if (newUser.ipath2_id == 1)
                    {
                        var adminRole = await rm.FindByNameAsync("Admin");
                        await um.AddToRoleAsync(newUser, adminRole.Name);
                    }

                    // Translation Editors
                    if (u.status.HasFlag(eUSERSTAT.LANGEDIT))
                    {
                        var tranlatorRole = await rm.FindByNameAsync("Translator");
                        await um.AddToRoleAsync(newUser, tranlatorRole.Name);
                    }

                    bulk.Add(newUser);
                    c++;
                    OnProgress((int)((float)c * 100 / userCount), $"{c}/{userCount}");

                    if (c % BulkSize == 0)
                    {
                        await BulkInsertAsync(newDb, bulk, ct);
                        bulk.Clear();
                    }
                }

                catch (Exception ex)
                {
                    OnMessage($"Error reading user {u.id}: {ex.Message}");
                    continue; // skip this user
                }
            }
        }

        if (bulk.Any()) await BulkInsertAsync(newDb, bulk, ct);
        bulk.Clear();

        var newCount = newDb.Users.Count();

        if (newCount != oldCount)
        {
            throw new Exception("not all users have been imported");
        }

        OnMessage($"{newCount} Users, {c} imported");
    }


    public async Task ImportCommunitiesAsync(CancellationToken ctk = default)
    {
        var list = await oldDb.Set<i2community>()
            .Where(c => !DataImportExtensions.communityIds.Keys.Contains(c.id))
            .ToListAsync();

        OnMessage($"exporting {list.Count} communities ... ");
        var newList = list.Select(o => o.ToNewEntity()).ToList();

        OnMessage("saving changes ...");
        await BulkInsertAsync(newDb, newList, ctk);
        OnProgress(100, $"{list.Count}/{newList.Count}");

        OnMessage($"{newList.Count()} communities imported");
    }




    public async Task<bool> ImportGroupsAsync(CancellationToken ct = default)
    {
        Console.WriteLine("group keys: " + DataImportExtensions.groupIds.Keys.Count().ToString());

        var q = oldDb.Set<i2group>()
            .Where(g => g.id > 1);

        if (DataImportExtensions.groupIds.Keys.Count() > 0)
        {
            q = q.Where(g => !DataImportExtensions.groupIds.Keys.Contains(g.id));
        }

        q = q.Include(g => g.members)
            .Include(g => g.communities)
            .OrderBy(g => g.id)
            .AsNoTracking();
        // .AsSplitQuery();

        var total = await q.CountAsync();

        var c = 0;
        var bulk = new List<Group>();
        OnMessage($"exporting {total} groups (bulk size = {BulkSize}) ... ");
        await foreach (var group in q.AsAsyncEnumerable())
        {
            if (ct.IsCancellationRequested)
            {
                ct.ThrowIfCancellationRequested();
            }

            // entity
            var n = group.ToNewEntity();

            if (!DataImportExtensions.groupIds.ContainsKey(group.id))
                DataImportExtensions.groupIds.Add(group.id, n.Id);

            // members
            n.Members ??= new List<GroupMember>();
            foreach (var gm in group.members)
            {
                // validate that the user_id is valid (contained in ownerCache)
                if (DataImportExtensions.userIds.ContainsKey(gm.user_id) && !n.Members.Any(m => m.UserId == DataImportExtensions.NewUserId(gm.user_id).Value))
                {
                    // old status => role: 0=member, 4=moderator, 2=inactive, 8=guest
                    // new => user can have only one role in a group
                    eMemberRole role = eMemberRole.User;
                    if ((gm.status & 4) != 0) role = eMemberRole.Moderator;
                    if ((gm.status & 2) != 0) role = eMemberRole.Inactive;
                    if ((gm.status & 8) != 0) role = eMemberRole.Guest;

                    var newGM = new GroupMember()
                    {
                        GroupId = DataImportExtensions.NewGroupId(gm.group_id).Value,
                        UserId = DataImportExtensions.NewUserId(gm.user_id).Value,
                        Role = role
                    };
                    n.Members.Add(newGM);
                }
                else
                {
                    Console.WriteLine("invalid member: " + gm.ToString());
                }
            }

            // count up
            c++;
            OnProgress((int)((float)c * 100 / total), $"{c}/{total}");

            bulk.Add(n);

            if (c % BulkSize == 0)
            {
                await BulkInsertAsync(newDb, bulk, ct);
                bulk.Clear();
            }
        }

        OnMessage($"{c} groups & membership converted ... saving to database ... ");
        if (bulk.Any()) await BulkInsertAsync(newDb, bulk, ct);
        bulk.Clear();

        return true;
    }



    #region "-- Data Nodes --"

    public async Task<bool> ImportNodesAsync(bool deleteExisting, CancellationToken ctk = default)
    {
        var groupIds = await newDb.Groups.Where(g => g.ipath2_id.HasValue).Select(g => g.ipath2_id.Value).ToHashSetAsync();
        /*
        var groupIds = new HashSet<int>();
        groupIds.Add(1000);
        */

        // check for topparent links
        OnMessage("checking pre-migration ... ");
        var tc = await oldDb.Set<i2object>().CountAsync(o => o.topparent_id != null);
        if (tc == 0)
        {
            OnMessage("please execute the 'prepare-migration.sql' first");
            return false;
        }

        // get group list => all groups if nothin specified (ignore fix admin group with id = 1)
        OnMessage($"{groupIds.Count} groups to be exported");

        // import over IAsyncEnumerable
        await ImportGroupNodesAsync(groupIds, deleteExisting, newDb, oldDb, ctk);

        // import over in memory paged list
        // await ImportGroupNodesListAsync(deleteExitingData, reImportAll, groupIds, newDb, oldDb, ctk);

        return true;
    }


    public async Task<bool> ImportGroupNodesAsync(HashSet<int> gid, bool deleteExitingData, iPathDbContext newDb, OldDB oldDb, CancellationToken ctk = default)
    {
        Stopwatch stopWatch = new Stopwatch();
        stopWatch.Start();

        if (deleteExitingData)
        {
            Console.WriteLine("delete imported node data");
            await newDb.NodeImports.ExecuteDeleteAsync();
            await newDb.Annotations.Where(a => a.ipath2_id.HasValue).ExecuteDeleteAsync();
            await newDb.Nodes.Where(n => n.ipath2_id.HasValue).ExecuteDeleteAsync();
            DataImportExtensions.nodeIds.Clear();
        }


        var q = oldDb.Set<i2object>()
            .Where(o => o.objclass != "imic")
            .Where(o => o.group_id.HasValue && gid.Contains(o.group_id.Value))
            .Where(o => !o.parent_id.HasValue)
            .Where(o => o.sender_id.HasValue && o.sender_id > 0)
            .Where(o => !DataImportExtensions.nodeIds.Keys.Contains(o.id))
            // .Where(o => DataImportExtensions.userIds.Keys.Contains(o.sender_id.Value))
            .Include(o => o.ChildNodes)
            .Include(o => o.Annotations)
            .AsNoTracking()
            .AsSplitQuery()
            .AsQueryable();

        // if (!reImportAll) q = q.Where(o => !o.ExportTime.HasValue);

        var total = await q.CountAsync(ctk);
        OnMessage($"Starting import of {total} root objects ... ");

        var objects = q.OrderBy(o => o.id).AsAsyncEnumerable();

        // debug => BulkSize = 1;

        var nodeBulk = new List<Node>();
        var annotationBulk = new List<Annotation>();
        var importDataBulk = new List<NodeImport>();
        var count = 0;

        await foreach (var o in objects)
        {
            count++;

            if (ctk.IsCancellationRequested)
            {
                ctk.ThrowIfCancellationRequested();
            }

            // nodes (incl. ChildNodes)
            Console.WriteLine("Parent Node #{0}", o.id);

            // create new Ids
            o.CreateNewId();

            // convert root node
            var n = o.ToNewEntity();
            nodeBulk.Add(n);

            // data/info
            importDataBulk.AddRange(new NodeImport { NodeId = n.Id, Info = o.info, Data = o.data });

            // child nodes => sender mus be > 0 and there there must be something in the old data field
            if (o.ChildNodes != null && o.ChildNodes.Any())
            {
                foreach (var c in o.ChildNodes.Where(c => c.sender_id > 0))
                {
                    Console.WriteLine("- Child Node #{0} on Parent #{1}", c.id, o.id);
                    var nc = c.ToNewEntity();
                    nodeBulk.Add(nc);

                    // data/info
                    importDataBulk.AddRange(new NodeImport { NodeId = nc.Id, Info = c.info, Data = c.data });
                }
            }

            // annotations
            annotationBulk.AddRange(o.Annotations.Where(a => DataImportExtensions.userIds.Keys.Contains(a.sender_id)).Select(a => a.ToNewEntity()));

            OnProgress((int)(double)(count * 100 / total), $"{count} / {total}");

            if (count % BulkSize == 0)
            {
                // node Bulk
                OnMessage($"saving {nodeBulk.Count()} nodes (incl child nodes) ... ");
                await SaveNodeImportAsync(nodeBulk, newDb, oldDb, ctk);

                // annotations
                await BulkInsertAsync(newDb, annotationBulk, ctk);
                annotationBulk.Clear();

                // data/info
                await BulkInsertAsync(newDb, importDataBulk, ctk);
                importDataBulk.Clear();
            }
        }

        OnMessage("saving remaining changes ... ");
        await SaveNodeImportAsync(nodeBulk, newDb, oldDb, ctk);
        nodeBulk.Clear();

        await BulkInsertAsync(newDb, annotationBulk, ctk);
        annotationBulk.Clear();

        await BulkInsertAsync(newDb, importDataBulk, ctk);
        importDataBulk.Clear();

        stopWatch.Stop();
        TimeSpan ts = stopWatch.Elapsed;
        OnMessage($"{count} nodes exported in {ts.TotalSeconds}s");


        return true;
    }


    private async Task SaveNodeImportAsync(List<Node> bulk, iPathDbContext newDb, OldDB oldDb, CancellationToken ctk = default)
    {
        if (bulk != null && bulk.Any())
        {
            // create list of ids to update in old db
            var objIds = bulk.Select(n => n.ipath2_id).ToHashSet();

            // delete alread imported NodeDatan (data/info fields with old xml)
            // await newDb.Set<NodeImport>().Where(d => Microsoft.EntityFrameworkCore.EF.Constant(objIds).Contains(d.)).ExecuteDeleteAsync();

            // bulk insert in new db
            await BulkInsertAsync(newDb, bulk, ctk);

            bulk.Clear();

            // update export flag
            await oldDb.Set<i2object>()
                .Where(o => Microsoft.EntityFrameworkCore.EF.Constant(objIds).Contains(o.id))
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ExportTime, DateTime.UtcNow), ctk);

        }
    }


    public async Task ImportUserStatsAsync(CancellationToken ctk = default)
    {
        var ts = DateTime.UtcNow;

        // drop existing data
        OnMessage($"Deleting records in new DB ... ");
        await Task.Delay(100);
        await newDb.NodeLastVisits.ExecuteDeleteAsync();


        // read old data into memory
        OnMessage($"Reading old data for {DataImportExtensions.userIds.Count} users and {DataImportExtensions.nodeIds.Count} nodes ... ");
        var total = await oldDb.lastvisits.CountAsync();
        var data = oldDb.lastvisits.AsNoTracking().Where(v => v.user_id > 0 && v.object_id > 0).AsAsyncEnumerable(); 

        var bulk = new List<NodeLastVisit>();
        var bulkSize = 10_000;

        OnMessage($"Saving {total} Items ...");
        var count = 0;
        var skipped = 0;
        
        await foreach (var d in data)
        {
            if (ctk.IsCancellationRequested)
            {
                ctk.ThrowIfCancellationRequested();
            }

            if (DataImportExtensions.userIds.ContainsKey(d.user_id) && DataImportExtensions.nodeIds.ContainsKey(d.object_id))
            {
                var v = NodeLastVisit.Create(DataImportExtensions.NewUserId(d.user_id).Value, DataImportExtensions.NewNodeId(d.object_id).Value, d.visitdate.ToUniversalTime());
                bulk.Add(v);
                count++;
            }
            else
            {
                skipped++;
            }

            if (bulk.Count() >= bulkSize)
            {
                await newDb.BulkInsertAsync(bulk, cancellationToken: ctk);
                newDb.ChangeTracker.Clear();
                OnProgress((int)((float)count * 100 / total), $"{count}/{total}");

                bulk.Clear();
            }
        }

        if (bulk.Count() > 0)
        {
            await newDb.BulkInsertAsync(bulk, cancellationToken: ctk);
            // await newDb.SaveChangesAsync(ctk);
            newDb.ChangeTracker.Clear();
            bulk.Clear();
        }

        OnProgress(100, $"{total}/{total}");

        var dur = DateTime.UtcNow - ts;
        OnMessage($"{count} Data imported into NodeLastVisits in {dur.Humanize()}, {skipped} skiped");

    }

    #endregion



    private async Task BulkInsertAsync<T>(DbContext ctx, List<T> bulk, CancellationToken ctk = default) where T : class, IBaseEntity
    {
        var sw = new Stopwatch();
        sw.Start();
        using (var transaction = ctx.Database.BeginTransaction())
        {
            var entityType = ctx.Model.FindEntityType(typeof(T));
            var schema = entityType.GetSchema();
            string? tableName = entityType.GetTableName();

            // check for update or insert
            var bulkIds = bulk.Select(x => x.Id).ToHashSet();
            var dbIds = await ctx.Set<T>().Where(e => Microsoft.EntityFrameworkCore.EF.Constant(bulkIds).Contains(e.Id)).Select(e => e.Id).ToListAsync();

            var insertBulk = bulk.Where(b => !dbIds.Contains(b.Id)).ToList(); // list for insert
            var updateBulk = bulk.Where(b => dbIds.Contains(b.Id)).ToList();  // list for update

            OnMessage($"Saving {tableName}, Insert: {insertBulk.Count}, Update: {updateBulk.Count}");

            if (insertBulk != null && insertBulk.Any())
            {
                await ctx.Set<T>().AddRangeAsync(insertBulk, ctk);
            }
            if (updateBulk != null && updateBulk.Any())
            {
                await ctx.AddRangeAsync(updateBulk, ctk);
                ctx.UpdateRange(updateBulk);
            }


            /*
            if (ctx.Database.ProviderName == "SqlServer" && tableName != null)
            {
                // for SqlServer activiate Identity Insert
                await ctx.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT {tableName} ON;");
                await ctx.SaveChangesAsync(ctk);
                await ctx.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT {tableName} OFF;");
            }
            else
            {
                // for other ds just save
                await ctx.SaveChangesAsync(ctk);
            }
            */

            try
            {
                await ctx.SaveChangesAsync(ctk);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                foreach (var entry in ex.Entries)
                {
                    Console.WriteLine("conflict: " + entry.Metadata.Name);
                }
            }

            transaction.Commit();
        }

        // release entities from change tracker               
        ctx.ChangeTracker.Entries()
            .Where(e => e.State != EntityState.Detached)
            .ToList()
            .ForEach(e => e.State = EntityState.Detached);

        ctx.ChangeTracker.Clear();

        sw.Stop();
        OnMessage($"transaction time: {sw.ElapsedMilliseconds}ms");
    }
}

