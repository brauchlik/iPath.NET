using Ardalis.GuardClauses;
using Cyrillic.Convert;
using EFCore.BulkExtensions;
using Humanizer;
using iPath.Application.Features.Documents;
using iPath.EF.Core.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query.Internal;
using System.Collections.Generic;
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
        var c = new ServiceRequest()
        {
            Id = Guid.CreateVersion7(),
            NodeType = "test"
        };

        c.Owner = await newDb.Users.FirstOrDefaultAsync();
        c.Group = await newDb.Groups.FirstOrDefaultAsync();

        c.Description = new();
        c.Description.Title = "Test Case";

        var id = c.Id.ToString();
        await newDb.ServiceRequests.AddAsync(c);

        if (id != c.Id.ToString())
        {
            Console.WriteLine("UUID has changed on AddAsync {0} => {1}", id, c.Id);
        }
        else
        {
            await newDb.SaveChangesAsync();
            Console.WriteLine("node #{0} saved", id);

            newDb.ServiceRequests.Remove(c);
            await newDb.SaveChangesAsync();
            Console.WriteLine("node #{0} deleted", id);
        }
    }


    public async Task<string> GetUserNameChars()
    {
        var sb = new StringBuilder();
        sb.Append("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+");
        var conv = new Conversion();

        await foreach (var username in oldDb.persons.Select(p => p.username).ToAsyncEnumerable())
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

        var failed = new List<i2person>();

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
                        throw new Exception(res.Errors.FirstOrDefault().Description);


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
                    failed.Add(u);
                    OnMessage($"Error reading user {u.username} #{u.id}: {ex.Message}");
                    continue; // skip this user
                }
            }
        }

        if (bulk.Any()) await BulkInsertAsync(newDb, bulk, ct);
        bulk.Clear();

        var newCount = newDb.Users.Count();

        if (newCount != oldCount)
        {
            Console.WriteLine($"not all users have been imported. {failed.Count} failed");
        }

        OnMessage($"{newCount} Users, {c} imported");

        // reload User IDs
        await DataImportExtensions.InitUserIdDictAsync(newDb);
    }


    public async Task ImportCommunitiesAsync(CancellationToken ctk = default)
    {
        if (!DataImportExtensions.AdminUserId.HasValue)
        {
            DataImportExtensions.AdminUserId = newDb.Users.First(u => u.UserName.ToLower() == "admin").Id;
        }

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
        if (!DataImportExtensions.AdminUserId.HasValue)
        {
            DataImportExtensions.AdminUserId = newDb.Users.First(u => u.UserName.ToLower() == "admin").Id;
        }
        if (!DataImportExtensions.DefaultCommunityId.HasValue)
        {
            DataImportExtensions.DefaultCommunityId = newDb.Communities.First().Id;
        }

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

            newDb.Groups.Add(n);
            await newDb.SaveChangesAsync(ct);

            // members
            foreach (var gm in group.members)
            {
                // validate that the user_id is valid (contained in ownerCache)
                var nUserId = DataImportExtensions.NewUserId(gm.user_id);
                if (nUserId.HasValue && !n.Members.Any(m => m.UserId == nUserId.Value))
                {
                    var xu1 = await newDb.Users.FirstOrDefaultAsync(u => u.Id == nUserId.Value);
                    Guard.Against.Null(xu1);

                    // old status => role: 0=member, 4=moderator, 2=inactive, 8=guest
                    // new => user can have only one role in a group
                    eMemberRole role = eMemberRole.User;
                    if ((gm.status & 4) != 0) role = eMemberRole.Moderator;
                    if ((gm.status & 2) != 0) role = eMemberRole.Banned;
                    if ((gm.status & 8) != 0) role = eMemberRole.Guest;

                    n.AddMember(xu1.Id, role);
                    /*
                    try
                    {
                        await newDb.SaveChangesAsync();
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine($"Error adding User {gm.user_id} to group {n.Name}");
                        var xu = await newDb.Users.FindAsync(nUserId);
                        var xu2 = await newDb.Users.FirstOrDefaultAsync(u => u.ipath2_id == gm.user_id);
                    }
                    */
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
                try
                {
                    await BulkInsertAsync(newDb, bulk, ct);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                bulk.Clear();
            }
        }

        OnMessage($"{c} groups & membership converted ... saving to database ... ");
        if (bulk.Any()) await BulkInsertAsync(newDb, bulk, ct);
        bulk.Clear();

        return true;
    }



    #region "-- Data Nodes --"

    public async Task<bool> ImportRequestsAsync(bool deleteExisting, CancellationToken ctk = default)
    {
        if (deleteExisting)
        {
            await newDb.DocumentImports.ExecuteDeleteAsync();
            await newDb.Annotations.ExecuteDeleteAsync();
            await newDb.ServiceRequestImports.ExecuteDeleteAsync();
        }


        var total = await newDb.Groups.CountAsync();
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
        await ÏmportRequestsAsync(groupIds, deleteExisting, newDb, oldDb, ctk);

        // import over in memory paged list
        // await ImportGroupNodesListAsync(deleteExitingData, reImportAll, groupIds, newDb, oldDb, ctk);

        return true;
    }


    public async Task<bool> ÏmportRequestsAsync(HashSet<int> gid, bool deleteExitingData, iPathDbContext newDb, OldDB oldDb, CancellationToken ctk = default)
    {
        Stopwatch stopWatch = new Stopwatch();
        stopWatch.Start();

        if (deleteExitingData)
        {
            Console.WriteLine("delete imported node data");
            await newDb.DocumentImports.ExecuteDeleteAsync();
            await newDb.Documents.Where(n => n.ipath2_id.HasValue).ExecuteDeleteAsync();
            await newDb.Annotations.Where(a => a.ipath2_id.HasValue).ExecuteDeleteAsync();
            await newDb.ServiceRequestImports.ExecuteDeleteAsync();
            await newDb.ServiceRequests.Where(n => n.ipath2_id.HasValue).ExecuteDeleteAsync();
            DataImportExtensions.nodeIds.Clear();
        }


        var q = oldDb.Set<i2object>()
            .Include(o => o.Annotations)
            .Where(o => o.objclass != "imic")
            .Where(o => o.group_id.HasValue && gid.Contains(o.group_id.Value))
            .Where(o => !o.parent_id.HasValue)
            .Where(o => o.sender_id.HasValue && o.sender_id > 0)
            .Where(o => !DataImportExtensions.nodeIds.Keys.Contains(o.id))
            .AsQueryable();

        // if (!reImportAll) q = q.Where(o => !o.ExportTime.HasValue);

        var total = await q.CountAsync(ctk);
        OnMessage($"Starting import of {total} root objects => ServiceRequest ... ");

        var objectsQuery = q.OrderBy(o => o.id);

        // debug => BulkSize = 1;

        var requestBulk = new List<ServiceRequest>();
        var requestImportDataBulk = new List<ServiceRequestImport>();
        var annotationBulk = new List<Annotation>();
        var count = 0;

        await foreach (var o in objectsQuery.AsAsyncEnumerable().WithCancellation(ctk))
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

            // convert root node => ServiceReqeust
            var n = o.ToServiceRequest();
            requestBulk.Add(n);

            // data/info
            requestImportDataBulk.AddRange(new ServiceRequestImport { Id = Guid.CreateVersion7(), ServiceRequestId = n.Id, Info = o.info, Data = o.data });


            // annotations
            annotationBulk.AddRange(o.Annotations.Where(a => DataImportExtensions.userIds.Keys.Contains(a.sender_id)).Select(a => a.ToNewEntity()));

            OnProgress((int)(double)(count * 100 / total), $"{count} / {total}");

            if (count % BulkSize == 0)
            {
                // node Bulk
                OnMessage($"saving {requestBulk.Count()} nodes (incl child nodes) ... ");
                await SaveNodeImportAsync(requestBulk, newDb, oldDb, ctk);
                requestBulk.Clear();

                // data/info
                await BulkInsertAsync(newDb, requestImportDataBulk, ctk);
                requestImportDataBulk.Clear();

                // annotations
                await BulkInsertAsync(newDb, annotationBulk, ctk);
                annotationBulk.Clear();
            }
        }

        OnMessage("saving remaining changes ... ");
        await SaveNodeImportAsync(requestBulk, newDb, oldDb, ctk);
        requestBulk.Clear();

        await BulkInsertAsync(newDb, annotationBulk, ctk);
        annotationBulk.Clear();

        await BulkInsertAsync(newDb, requestImportDataBulk, ctk);
        requestImportDataBulk.Clear();

        stopWatch.Stop();
        TimeSpan ts = stopWatch.Elapsed;
        OnMessage($"{count} nodes imported as ServiceRequests in {ts.TotalSeconds}s");


        return true;
    }


    private async Task SaveNodeImportAsync(List<ServiceRequest> bulk, iPathDbContext newDb, OldDB oldDb, CancellationToken ctk = default)
    {
        if (bulk != null && bulk.Any())
        {
            // create list of ids to update in old db
            var objIds = bulk.Select(n => n.ipath2_id).ToHashSet();

            // delete alread imported NodeDatan (data/info fields with old xml)
            // await newDb.Set<NodeImport>().Where(d => Microsoft.EntityFrameworkCore.EF.Constant(objIds).Contains(d.)).ExecuteDeleteAsync();

            // bulk insert in new db
            try
            {
                await BulkInsertAsync(newDb, bulk, ctk);
            }
            catch(Exception x)
            {
                throw x;
            }

            bulk.Clear();

            // update export flag
            /*
            await oldDb.Set<i2object>()
                .Where(o => Microsoft.EntityFrameworkCore.EF.Constant(objIds).Contains(o.id))
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ExportTime, DateTime.UtcNow), ctk);
            */
        }
    }





    public async Task<bool> ÏmportDocumentsAsync(bool deleteExitingData, CancellationToken ctk = default)
    {
        Stopwatch stopWatch = new Stopwatch();
        stopWatch.Start();

        if (deleteExitingData)
        {
            Console.WriteLine("delete imported documents data");
            await newDb.DocumentImports.ExecuteDeleteAsync();
            await newDb.Documents.Where(n => n.ipath2_id.HasValue).ExecuteDeleteAsync();
            var nc = await newDb.Documents.CountAsync();
            Console.WriteLine($"statring from {nc} documents.");
        }


        int importCount = 0;
        int level = 1;
        var parentIda = DataImportExtensions.nodeIds.Keys.ToHashSet();
        while (parentIda.Any())
        {
            Console.WriteLine($"--------------------------------------------------------------------");
            Console.WriteLine($"Importing Child-Nodes, Level {level} ....");
            var tmp = await ImportDocumentsAsync(parentIda, ctk);
            parentIda = tmp;
            importCount += tmp.Count();
            Console.WriteLine($"Imported {tmp.Count} Documents in Level {level}");
            level++;
        }

        stopWatch.Stop();
        Console.WriteLine($"Imported {importCount} Documents in {stopWatch.Elapsed.TotalSeconds}s");

        return true;
    }




    public async Task<HashSet<int>> ImportDocumentsAsync(HashSet<int> parentIds, CancellationToken ctk = default)
    {
        var q = oldDb.Set<i2object>()
            .AsNoTracking()
            .Where(o => !DataImportExtensions.docIds.Keys.Contains(o.id))
            .Where(o => o.parent_id.HasValue && parentIds.Contains(o.parent_id.Value))
            .Where(o => o.sender_id.HasValue && o.sender_id > 0)
            .AsQueryable();

        var total = await q.CountAsync(ctk);
        OnMessage($"Starting import of {total} child objects => Documents ... ");

        // BulkSize = 100;
        // var objectsQuery = q.OrderBy(o => o.id).Skip(4500);
        var objectsQuery = q.OrderBy(o => o.id);

        var docBuklBulk = new List<DocumentNode>();
        var docImportDataBulk = new List<DocumentImport>();
        var count = 0;

        var importedIds = new HashSet<int>();

        await foreach (var o in objectsQuery.AsAsyncEnumerable().WithCancellation(ctk))
        {
            count++;

            if (ctk.IsCancellationRequested)
            {
                ctk.ThrowIfCancellationRequested();
            }

            // nodes (incl. ChildNodes)
            Debug.WriteLine("Child Node #{0}", o.id);
            importedIds.Add(o.id);

            // create new Ids
            o.CreateNewDocId();

            // convert root node => ServiceReqeust
            var n = o.ToDocument();
            if (n is not null)
            {
                docBuklBulk.Add(n);

                // data/info
                docImportDataBulk.AddRange(new DocumentImport { Id = Guid.CreateVersion7(), DocumentId = n.Id, Info = o.info, Data = o.data });
            }

            // OnProgress((int)(double)(count * 100 / total), $"{count} / {total}");

            if (count % BulkSize == 0)
            {
                OnProgress((int)(double)(count * 100 / total), $"{count} / {total}");

                // node Bulk
                await BulkInsertAsync(newDb, docBuklBulk, ctk);
                docBuklBulk.Clear();

                // data/info
                await BulkInsertAsync(newDb, docImportDataBulk, ctk);
                docImportDataBulk.Clear();
            }
        }

        OnMessage("saving remaining changes ... ");
        // node Bulk
        await BulkInsertAsync(newDb, docBuklBulk, ctk);
        docBuklBulk.Clear();

        // data/info
        await BulkInsertAsync(newDb, docImportDataBulk, ctk);
        docImportDataBulk.Clear();


        return importedIds;
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

        var bulk = new List<ServiceRequestLastVisit>();
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
                var v = ServiceRequestLastVisit.Create(DataImportExtensions.NewUserId(d.user_id).Value, DataImportExtensions.NewNodeId(d.object_id).Value, d.visitdate.ToUniversalTime());
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
        using var transaction = await ctx.Database.BeginTransactionAsync();
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
        catch (Exception e2)
        {
            Console.WriteLine(e2.InnerException?.Message);
            Console.WriteLine(e2.Message);
            throw e2;
        }
        await transaction.CommitAsync();

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

