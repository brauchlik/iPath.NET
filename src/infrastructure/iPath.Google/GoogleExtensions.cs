using Ardalis.GuardClauses;
using iPath.Domain.Entities;
using iPath.Google.Storage;

namespace iPath.Google;

public static class GoogleExtensions
{
    public static bool IsGoogle(this StorageInfo s)
    {
        return s is not null && s.ProviderName == GoogleDriveStorageService.GoogleDriveName && !string.IsNullOrEmpty(s.StorageId);
    }

    public static void AssertGoogle(this StorageInfo s)
    {
        if (!s.IsGoogle())
        {
            throw new NotFoundException(s.StorageId, "no google file");
        }
    }

    public static void AssertGoogle(this DocumentNode d)
    {
        if (!d.File.Storage.IsGoogle())
        {
            throw new NotFoundException(d.Id.ToString(), "no google file");
        }
    }
}
