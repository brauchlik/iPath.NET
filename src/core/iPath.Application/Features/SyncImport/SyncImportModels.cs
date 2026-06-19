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

public class GroupImportStatus
{
    public string GroupName { get; set; } = "";
    public int OldRootCount { get; set; }
    public int SyncedRootCount { get; set; }
    public int RemainingRootCount => OldRootCount - SyncedRootCount;
    public int AnnotationCount { get; set; }
    public int UserCount { get; set; }
}

public class OldAnnotationDto
{
    public int Id { get; set; }
    public int Sender_id { get; set; }
    public int Object_id { get; set; }
    public byte[]? Data { get; set; }
    public DateTime Entered { get; set; }
}

public class OldLastVisitDto
{
    public int Id { get; set; }
    public int User_id { get; set; }
    public int Object_id { get; set; }
    public DateTime Visitdate { get; set; }
}

public record GroupImportResult(int RootsImported, string Message, bool WasReimport = false);

public class SyncJobState
{
    public Guid JobId { get; init; } = Guid.CreateVersion7();
    public int GroupId { get; init; }
    public int Current { get; set; }
    public int Total { get; set; }
    public string Status { get; set; } = "Starting...";
    public bool IsRunning => !IsDone && Error is null;
    public bool IsDone { get; set; }
    public string? Error { get; set; }
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
}

public interface ISyncJobManager
{
    SyncJobState? Current { get; }
    Guid StartSync(int groupId);
    Guid StartReimport(int groupId);
}

public interface ISyncImportRunner
{
    Task<List<OldGroupSummary>> GetOldGroupSummariesAsync(CancellationToken ct = default);
    Task<int> SyncCommunitiesAndGroupsAsync(CancellationToken ct = default);
    Task<SyncStartResponse> SyncGroupAsync(SyncStartRequest request, CancellationToken ct = default);
    Task<SyncStartResponse> SyncGroupWithProgressAsync(int groupId, IProgress<(int Current, int Total, string Status)> progress, CancellationToken ct = default);
    Task<GroupImportResult> ReimportGroupAsync(int groupId, IProgress<(int Current, int Total, string Status)>? progress = null, CancellationToken ct = default);
    Task<int> ImportUsersAsync(CancellationToken ct = default);
    Task<SyncStartResponse> SyncGroupsAsync(int[] groupIds, CancellationToken ct = default);
    Task<int> ImportLastVisitsAsync(int[] groupIds, CancellationToken ct = default);
    Task<GroupImportStatus> GetGroupImportStatusAsync(int groupId, CancellationToken ct = default);
}
