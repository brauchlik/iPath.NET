namespace iPath.Domain.Entities;

public class StorageInfo
{
    public string ProviderName { get; set; }
    public string StorageId { get; set; }
    public DateTime? UpdatedOn { get; set; }

    public StorageInfo()
    {
    }

    public StorageInfo(string provider, string id)
    {
        ProviderName = provider;
        StorageId = id;
        UpdatedOn = DateTime.UtcNow;
    }
}




public static class StorageInforExtension
{
    extension (List<StorageInfo>? info)
    {
        public string? GetStorageId(string ProviderName) => info?.FirstOrDefault(x => x.ProviderName == ProviderName)?.StorageId;
        public StorageInfo? ByProvider(string ProviderName) => info?.FirstOrDefault(x => x.ProviderName == ProviderName);
    }

}