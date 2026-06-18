namespace iPath.Application.Features.SyncImport;

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

public class OldGroupSummary
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime Entered { get; set; }
    public int RootNodeCount { get; set; }
}

public record SyncStartRequest(int GroupId);

public record SyncStartResponse(string JobId);

public interface ISyncImportRunner
{
    Task<List<OldGroupSummary>> GetOldGroupSummariesAsync(CancellationToken ct = default);
    Task<SyncStartResponse> SyncGroupAsync(SyncStartRequest request, CancellationToken ct = default);
}
