using iPath.Application.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace iPath.Test.xUnit2.Localization;

public class TranslationDefaultsServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _shipped;
    private readonly string _live;

    private static readonly string[] Cultures = ["en", "de"];

    public TranslationDefaultsServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "iPathLocTest_" + Guid.NewGuid().ToString("N"));
        _shipped = Path.Combine(_root, "shipped");
        _live = Path.Combine(_root, "live");
        Directory.CreateDirectory(_shipped);
        Directory.CreateDirectory(_live);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { }
    }

    private static LocalizationFileService FileService(string root, string[] cultures) =>
        new(Options.Create(new LocalizationSettings { LocalesRoot = root, SupportedCultures = cultures }),
            NullLogger<LocalizationFileService>.Instance);

    private static void WriteLocale(string root, string locale, params (string Key, string Value)[] words)
    {
        var svc = FileService(root, Cultures);
        var data = svc.GetTranslationData(locale);
        foreach (var (key, value) in words)
        {
            data.Words[key] = value;
        }
        svc.SaveTranslation(data);
    }

    private TranslationDefaultsService Service(string? liveRoot = null, string? shippedRoot = null) =>
        new(Options.Create(new LocalizationSettings
            {
                LocalesRoot = liveRoot ?? _live,
                DefaultsRoot = shippedRoot ?? _shipped,
                SupportedCultures = Cultures
            }),
            NullLoggerFactory.Instance);

    [Fact]
    public void Import_Adds_Missing_Fills_Empty_And_Never_Overwrites()
    {
        WriteLocale(_shipped, "de", ("A", "A-de"), ("B", "B-de"), ("C", "C-de"));
        WriteLocale(_live, "de", ("A", "A-live"), ("B", ""));

        var summary = Service().Import();

        var de = summary.Locales.Single(x => x.Locale == "de");
        de.Added.Should().Be(1);
        de.Filled.Should().Be(1);

        var result = FileService(_live, Cultures).GetTranslationData("de");
        result.Words["A"].Should().Be("A-live", "an existing translation is never overwritten");
        result.Words["B"].Should().Be("B-de", "an empty value is filled from the shipped default");
        result.Words["C"].Should().Be("C-de", "a missing key is added from the shipped default");
    }

    [Fact]
    public void Import_Reports_No_Changes_When_Already_In_Sync()
    {
        WriteLocale(_shipped, "de", ("A", "A-de"));
        WriteLocale(_live, "de", ("A", "A-live"));

        var summary = Service().Import();

        var de = summary.Locales.Single(x => x.Locale == "de");
        de.Added.Should().Be(0);
        de.Filled.Should().Be(0);
    }

    [Fact]
    public void Import_Is_NoOp_When_Live_Store_Is_The_Shipped_Store()
    {
        WriteLocale(_shipped, "de", ("A", "A-de"), ("B", "B-de"));

        var svc = Service(liveRoot: _shipped, shippedRoot: _shipped);
        svc.IsLiveStoreSeparate.Should().BeFalse();

        var summary = svc.Import();
        summary.IsLiveStoreSeparate.Should().BeFalse();
        summary.TotalAdded.Should().Be(0);
        summary.TotalFilled.Should().Be(0);
    }

    [Fact]
    public void IsLiveStoreSeparate_Is_True_When_Roots_Differ()
    {
        WriteLocale(_shipped, "de", ("A", "A-de"));
        Service().IsLiveStoreSeparate.Should().BeTrue();
    }

    [Fact]
    public void GetDefaults_Returns_Shipped_Values()
    {
        WriteLocale(_shipped, "de", ("A", "A-de"));

        Service().GetDefaults("de").Words["A"].Should().Be("A-de");
    }

    [Fact]
    public void SaveTranslation_Refuses_Unsupported_Locale()
    {
        var svc = FileService(_root, ["en"]);
        var data = svc.GetTranslationData("en");
        data.locale = "de";
        data.Words["X"] = "Y";

        svc.SaveTranslation(data).Should().BeFalse();
        File.Exists(Path.Combine(_root, "de.json")).Should().BeFalse();
    }

    [Fact]
    public void SaveTranslation_Refuses_Path_Traversal_Locale()
    {
        var svc = FileService(_root, ["en"]);
        var data = svc.GetTranslationData("en");
        data.locale = "../evil";

        svc.SaveTranslation(data).Should().BeFalse();
        File.Exists(Path.Combine(_root, "..", "evil.json")).Should().BeFalse();
    }

    [Fact]
    public void SaveTranslation_Writes_Supported_Locale()
    {
        var svc = FileService(_root, ["en"]);
        var data = svc.GetTranslationData("en");
        data.Words["X"] = "Y";

        svc.SaveTranslation(data).Should().BeTrue();
        File.Exists(Path.Combine(_root, "en.json")).Should().BeTrue();
    }
}
