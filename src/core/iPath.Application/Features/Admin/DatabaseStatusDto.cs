namespace iPath.Application.Features.Admin;

public class DatabaseStatusDto
{
    public string? LastMigration { get; set; } = null;
    public string? InitialAdminPassword { get; set; } = null;
    public string? ProviderName { get; set; }
    public string? ConnectionString { get; set; }
    public string? DbFileSize { get; set; }
    public bool AutoMigrate { get; set; }
    public List<MigrationItemDto> AppliedMigrations { get; set; } = [];
    public List<MigrationItemDto> PendingMigrations { get; set; } = [];
}

public class MigrationItemDto
{
    public string Name { get; set; } = "";
    public string Created { get; set; } = "";
}

public class TableRowCountDto
{
    public string TableName { get; set; } = "";
    public long RowCount { get; set; }
}
