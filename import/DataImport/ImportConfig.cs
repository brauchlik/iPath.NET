namespace iPath.DataImport;

public class ImportConfig
{
    public int BulkSize { get; set; }

    public bool ImportUsers { get; set; }
    public bool ImportCommunities { get; set; }
    public bool ImportGroups { get; set; }
    public bool ImportNodes { get; set; }
    public bool ImportNodeStats { get; set; }
}