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
}
