using Dapper;
using iPath.Application.Features.SyncImport;
using MySqlConnector;
using System.Data;

namespace iPath.API.Services.SyncImport;

public class OldDataService(string connectionString)
{
    private IDbConnection CreateConnection() => new MySqlConnection(connectionString);

    // Helper: CAST(CONVERT(x USING latin1) AS BINARY) unwraps the double-encoded UTF-8
    // PHP stored UTF-8 bytes via a latin1 connection; MySQL converted them to the
    // column's utf8 charset by interpreting the bytes as latin1 first.
    // This CAST chain reverses that: forces the bytes back to latin1, then returns
    // them as raw bytes so C# can decode them as proper UTF-8.
    private const string BinaryDecode = "CAST(CONVERT(data USING latin1) AS BINARY)";
    private const string InfoDecode = "CAST(CONVERT(info USING latin1) AS BINARY)";

    public async Task<List<OldGroupDto>> GetGroupsAsync(CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = $"SELECT id, name, {InfoDecode} AS info, entered FROM groups";
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

    public async Task<List<OldObjectDto>> GetRootObjectsAsync(HashSet<int> groupIds, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = $"SELECT id, class AS ObjClass, " +
                  $"{BinaryDecode} AS data, {InfoDecode} AS info, " +
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
                  $"{BinaryDecode} AS data, {InfoDecode} AS info, " +
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
        var sql = "SELECT id, email, username, password, status, creator, entered, data, info FROM person";
        return (await conn.QueryAsync<OldPersonDto>(sql)).ToList();
    }

    public async Task<int> CountChildObjectsForGroupAsync(int groupId, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = "SELECT COUNT(*) FROM objects o " +
                  "JOIN objects r ON o.parent_id = r.id " +
                  "WHERE r.parent_id IS NULL AND r.group_id = @groupId";
        return await conn.ExecuteScalarAsync<int>(sql, new { groupId });
    }

    public async Task<int> CountAnnotationsForGroupAsync(int groupId, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = "SELECT COUNT(*) FROM annotation a " +
                  "JOIN objects o ON a.object_id = o.id " +
                  "WHERE o.group_id = @groupId";
        return await conn.ExecuteScalarAsync<int>(sql, new { groupId });
    }

    public async Task<OldGroupDto?> GetGroupAsync(int groupId, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = "SELECT id, name, info, entered FROM groups WHERE id = @id";
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
        var sql = "SELECT id, email, username, password, status, creator, entered, data, info FROM person WHERE id = @id";
        return await conn.QueryFirstOrDefaultAsync<OldPersonDto>(sql, new { id = personId });
    }

    public async Task<List<OldAnnotationDto>> GetAnnotationsForObjectsAsync(HashSet<int> objectIds, int minId, CancellationToken ct = default)
    {
        if (!objectIds.Any()) return [];

        using var conn = CreateConnection();
        var sql = "SELECT id, sender_id, object_id, data, entered FROM annotation " +
                  "WHERE object_id IN @objectIds AND id > @minId";
        return (await conn.QueryAsync<OldAnnotationDto>(sql, new { objectIds, minId })).ToList();
    }

    public async Task<List<OldLastVisitDto>> GetLastVisitsForGroupsAsync(int[] groupIds, CancellationToken ct = default)
    {
        if (groupIds.Length == 0) return [];

        using var conn = CreateConnection();
        var sql = "SELECT lv.Id, lv.user_id, lv.object_id, lv.visitdate " +
                  "FROM lastvisit lv " +
                  "JOIN objects o ON lv.object_id = o.id " +
                  "WHERE o.group_id IN @groupIds";
        return (await conn.QueryAsync<OldLastVisitDto>(sql, new { groupIds })).ToList();
    }
}
