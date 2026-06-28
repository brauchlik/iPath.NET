using System.IO.Compression;
using iPath.Application.Features.Conversion;
using iPath.Domain.Config;
using Microsoft.Extensions.Logging;
using NSubstitute;
using FluentAssertions;

namespace iPath.Test.xUnit2.SystemCleanup;

public class SystemCleanupTests
{
    [Fact]
    public void SystemCleanupConfig_HasValidDefaults()
    {
        var config = new SystemCleanupConfig();
        config.Enabled.Should().BeTrue();
        config.TimeOfDay.Should().Be("03:00");
        config.PurgeDeletedDocuments.Should().BeFalse();
        config.CleanStaleCache.Should().BeTrue();
        config.StaleCacheDays.Should().Be(7);
        config.CleanStaging.Should().BeTrue();
        config.StaleStagingDays.Should().Be(2);
    }

    [Theory]
    [InlineData("03:00", "2026-06-28T01:00:00", 2)] // Same day, target not yet passed (1 AM -> 3 AM is 2 hours)
    [InlineData("03:00", "2026-06-28T03:00:00", 24)] // Target is exactly now, schedules for tomorrow (24 hours)
    [InlineData("03:00", "2026-06-28T05:00:00", 22)] // Target passed today, schedules for tomorrow (5 AM -> 3 AM tomorrow is 22 hours)
    [InlineData("invalid", "2026-06-28T01:00:00", 2)] // Invalid fallback to 03:00 (1 AM -> 3 AM is 2 hours)
    public void CalculateNextDelay_ComputesCorrectTimeRemaining(string configTime, string nowStr, double expectedHours)
    {
        // Arrange
        var now = DateTime.Parse(nowStr);

        // Act
        var delay = iPath.API.Services.Jobs.SystemCleanupWorker.CalculateNextDelay(configTime, now, out _);

        // Assert
        delay.TotalHours.Should().Be(expectedHours);
    }

    [Fact]
    public void DziImportPlugin_CanHandleZip_ReturnsTrue_WhenDziIsAtRoot()
    {
        // Arrange
        var ipathConfig = Substitute.For<Microsoft.Extensions.Options.IOptions<iPathConfig>>();
        var logger = Substitute.For<ILogger<DziImportPlugin>>();
        var plugin = new DziImportPlugin(ipathConfig, logger);

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            archive.CreateEntry("test.dzi");
            archive.CreateEntry("test_files/1.webp");
        }

        memoryStream.Position = 0;
        using var readArchive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

        // Act
        var result = plugin.CanHandleZip(readArchive);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void DziImportPlugin_CanHandleZip_ReturnsTrue_WhenDziIsInSubdirectory()
    {
        // Arrange
        var ipathConfig = Substitute.For<Microsoft.Extensions.Options.IOptions<iPathConfig>>();
        var logger = Substitute.For<ILogger<DziImportPlugin>>();
        var plugin = new DziImportPlugin(ipathConfig, logger);

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            archive.CreateEntry("subdir/test.dzi");
        }

        memoryStream.Position = 0;
        using var readArchive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

        // Act
        var result = plugin.CanHandleZip(readArchive);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void DziImportPlugin_CanHandleZip_ReturnsFalse_WhenNoDziExists()
    {
        // Arrange
        var ipathConfig = Substitute.For<Microsoft.Extensions.Options.IOptions<iPathConfig>>();
        var logger = Substitute.For<ILogger<DziImportPlugin>>();
        var plugin = new DziImportPlugin(ipathConfig, logger);

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            archive.CreateEntry("test_files/1.webp");
        }

        memoryStream.Position = 0;
        using var readArchive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

        // Act
        var result = plugin.CanHandleZip(readArchive);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void DziImportPlugin_CanHandleZip_ReturnsFalse_WhenMultipleDziExist()
    {
        // Arrange
        var ipathConfig = Substitute.For<Microsoft.Extensions.Options.IOptions<iPathConfig>>();
        var logger = Substitute.For<ILogger<DziImportPlugin>>();
        var plugin = new DziImportPlugin(ipathConfig, logger);

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            archive.CreateEntry("test1.dzi");
            archive.CreateEntry("test2.dzi");
        }

        memoryStream.Position = 0;
        using var readArchive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

        // Act
        var result = plugin.CanHandleZip(readArchive);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void DziImportPlugin_CanHandleZip_ReturnsFalse_WhenDziIsTooDeep()
    {
        // Arrange
        var ipathConfig = Substitute.For<Microsoft.Extensions.Options.IOptions<iPathConfig>>();
        var logger = Substitute.For<ILogger<DziImportPlugin>>();
        var plugin = new DziImportPlugin(ipathConfig, logger);

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            // 2 levels deep
            archive.CreateEntry("dir1/dir2/test.dzi");
        }

        memoryStream.Position = 0;
        using var readArchive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

        // Act
        var result = plugin.CanHandleZip(readArchive);

        // Assert
        result.Should().BeFalse();
    }
}
