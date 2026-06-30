using System.Globalization;

namespace iPath.Domain.Config;

public class CacheSettings
{
    public const string ConfigName = "CacheSettings";

    public string MaxCacheSize { get; set; } = "10 GB";
    public string CheapRetention { get; set; } = "14d";
    public string ExpensiveRetention { get; set; } = "60d";
    public string DziTileCacheMode { get; set; } = "Disk";
    public string MemoryTileSliding { get; set; } = "5m";

    public long MaxCacheSizeBytes => HumanizerSizeToBytes(MaxCacheSize);
    public TimeSpan CheapRetentionSpan => HumanizerPeriodToTimeSpan(CheapRetention);
    public TimeSpan ExpensiveRetentionSpan => HumanizerPeriodToTimeSpan(ExpensiveRetention);
    public TimeSpan MemoryTileSlidingSpan => HumanizerPeriodToTimeSpan(MemoryTileSliding);

    private static long HumanizerSizeToBytes(string size)
    {
        if (string.IsNullOrWhiteSpace(size)) return 0;
        size = size.Trim().ToUpperInvariant();
        var parts = size.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return long.TryParse(size, out var n) ? n : 0;
        if (!double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
            return 0;
        return parts[1] switch
        {
            "B" => (long)num,
            "KB" => (long)(num * 1024),
            "MB" => (long)(num * 1024 * 1024),
            "GB" => (long)(num * 1024 * 1024 * 1024),
            "TB" => (long)(num * 1024L * 1024 * 1024 * 1024),
            _ => 0
        };
    }

    private static TimeSpan HumanizerPeriodToTimeSpan(string period)
    {
        if (string.IsNullOrWhiteSpace(period)) return TimeSpan.Zero;
        period = period.Trim().ToLowerInvariant();
        if (!double.TryParse(period.TrimEnd('d', 'h', 'm', 's'), NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
            return TimeSpan.Zero;
        return period[^1] switch
        {
            'd' => TimeSpan.FromDays(num),
            'h' => TimeSpan.FromHours(num),
            'm' => TimeSpan.FromMinutes(num),
            's' => TimeSpan.FromSeconds(num),
            _ => TimeSpan.FromDays(num)
        };
    }
}
