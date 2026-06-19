using Dapper;
using iPath.Application.Features.SyncImport;
using MySqlConnector;
using System.Data;

namespace iPath.API.Services.SyncImport;

public class OldDataService(string connectionString)
{
    private IDbConnection CreateConnection() => new MySqlConnection(connectionString);

    // Helper: CONVERT(CAST(CONVERT(x USING latin1) AS BINARY) USING utf8mb4) unwraps
    // the double-encoded UTF-8. PHP stored UTF-8 bytes via a latin1 connection;
    // MySQL converted them by interpreting the bytes as latin1 first.
    // This chain reverses that and returns a proper utf8mb4 string (not binary),
    // so MySqlConnector handles it as text regardless of connection charset.
    private const string DataDecode = "CONVERT(CAST(CONVERT(data USING latin1) AS BINARY) USING utf8mb4)";
    private const string InfoDecode = "CONVERT(CAST(CONVERT(info USING latin1) AS BINARY) USING utf8mb4)";
    private const string NameDecode = "CONVERT(CAST(CONVERT(name USING latin1) AS BINARY) USING utf8mb4)";
    private const string DescrDecode = "CONVERT(CAST(CONVERT(description USING latin1) AS BINARY) USING utf8mb4)";
    private const string UsernameDecode = "CONVERT(CAST(CONVERT(username USING latin1) AS BINARY) USING utf8mb4)";

    public async Task<List<OldGroupDto>> GetGroupsAsync(CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = $"SELECT id, {NameDecode} AS name, {InfoDecode} AS info, entered FROM groups";
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
        var sql = $"SELECT id, {NameDecode} AS name, {DescrDecode} AS description, created_by, created_on FROM community";
        return (await conn.QueryAsync<OldCommunityDto>(sql)).ToList();
    }

    public async Task<List<OldCommunityGroupDto>> GetCommunityGroupsAsync(CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = "SELECT community_id, group_id FROM community_group";
        return (await conn.QueryAsync<OldCommunityGroupDto>(sql)).ToList();
    }

    public async Task<List<OldObjectDto>> GetRootObjectsAsync(HashSet<int> groupIds, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = $"SELECT id, class AS ObjClass, " +
                  $"{DataDecode} AS data, {InfoDecode} AS info, " +
                  "entered, modified, group_id, parent_id, sender_id, sort_nr " +
                  "FROM objects " +
                  "WHERE class != 'imic' " +
                  "AND (parent_id IS NULL OR parent_id = 0 OR parent_id = -1) " +
                  "AND group_id > 0 " +
                  "AND sender_id > 0 " +
                  "AND group_id IN @groupIds";
        return (await conn.QueryAsync<OldObjectDto>(sql, new { groupIds })).ToList();
    }

    public async Task<List<OldObjectDto>> GetChildObjectsAsync(HashSet<int> parentIds, CancellationToken ct = default)
    {
        if (!parentIds.Any()) return [];

        using var conn = CreateConnection();
        var sql = $"SELECT id, class AS ObjClass, " +
                  $"{DataDecode} AS data, {InfoDecode} AS info, " +
                  "entered, modified, group_id, parent_id, sender_id, sort_nr " +
                  "FROM objects " +
                  "WHERE parent_id > 0 " +
                  "AND parent_id IN @parentIds";
        return (await conn.QueryAsync<OldObjectDto>(sql, new { parentIds })).ToList();
    }

    public async Task<int> CountRootObjectsAsync(int groupId, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = "SELECT COUNT(*) FROM objects " +
                  "WHERE (parent_id IS NULL OR parent_id = 0 OR parent_id = -1) " +
                  "AND group_id = @groupId AND sender_id > 0";
        return await conn.ExecuteScalarAsync<int>(sql, new { groupId });
    }

    public async Task<List<OldPersonDto>> GetPersonsAsync(CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = $"SELECT id, email, {UsernameDecode} AS username, password, status, creator, entered, " +
                  $"{DataDecode} AS data, {InfoDecode} AS info " +
                  "FROM person";
        return (await conn.QueryAsync<OldPersonDto>(sql)).ToList();
    }

    public async Task<int> CountChildObjectsForGroupAsync(int groupId, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = "SELECT COUNT(*) FROM objects o " +
                  "INNER JOIN objects r ON o.parent_id = r.id " +
                  "WHERE r.group_id = @groupId AND r.parent_id IS NULL";
        return await conn.ExecuteScalarAsync<int>(sql, new { groupId });
    }

    public async Task<int> CountAnnotationsForGroupAsync(int groupId, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = "SELECT COUNT(*) FROM annotation a " +
                  "INNER JOIN objects o ON o.id = a.object_id " +
                  "WHERE o.group_id = @groupId";
        return await conn.ExecuteScalarAsync<int>(sql, new { groupId });
    }

    public async Task<OldGroupDto?> GetGroupAsync(int groupId, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = $"SELECT id, {NameDecode} AS name, {InfoDecode} AS info, entered FROM groups WHERE id = @id";
        return await conn.QueryFirstOrDefaultAsync<OldGroupDto>(sql, new { id = groupId });
    }

    public async Task<List<OldGroupMemberDto>> GetGroupMembersForGroupAsync(int groupId, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = "SELECT group_id, user_id, status FROM group_member WHERE group_id = @groupId";
        return (await conn.QueryAsync<OldGroupMemberDto>(sql, new { groupId })).ToList();
    }

    public async Task<OldPersonDto?> GetPersonAsync(int personId, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = $"SELECT id, email, {UsernameDecode} AS username, password, status, creator, entered, " +
                  $"{DataDecode} AS data, {InfoDecode} AS info " +
                  "FROM person WHERE id = @id";
        return await conn.QueryFirstOrDefaultAsync<OldPersonDto>(sql, new { id = personId });
    }

    public async Task<List<OldAnnotationDto>> GetAnnotationsForObjectsAsync(HashSet<int> objectIds, int minId = 0, CancellationToken ct = default)
    {
        if (!objectIds.Any()) return [];

        using var conn = CreateConnection();
        var sql = $"SELECT id, sender_id, object_id, {DataDecode} AS data, entered " +
                  "FROM annotation WHERE object_id IN @objectIds AND id > @minId";
        return (await conn.QueryAsync<OldAnnotationDto>(sql, new { objectIds, minId })).ToList();
    }

    public async Task<List<OldLastVisitDto>> GetLastVisitsForGroupsAsync(HashSet<int> groupIds, CancellationToken ct = default)
    {
        if (!groupIds.Any()) return [];

        using var conn = CreateConnection();
        var sql = "SELECT lv.id, lv.user_id, lv.object_id, lv.visitdate " +
                  "FROM lastvisit lv " +
                  "INNER JOIN objects o ON o.id = lv.object_id " +
                  "WHERE o.group_id IN @groupIds AND lv.user_id > 0 AND lv.object_id > 0";
        return (await conn.QueryAsync<OldLastVisitDto>(sql, new { groupIds })).ToList();
    }
}
