using iPath.Application.Features.Documents;
using iPath.EF.Core.FeatureHandlers.Documents.Commands;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace iPath.Test.xUnit2.Documents;

public class WsiImportCommandHandlerTests : IDisposable
{
    private readonly List<string> _tempDirs = new();

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch { }
        }
    }

    private string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wsi-import-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    private static WsiImportCommandHandler CreateHandler(IMediator mediator)
        => new(mediator, NullLogger<WsiImportCommandHandler>.Instance);

    private static IMediator CreateMediator()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<UploadDocumentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DocumentDto>(null!));
        return mediator;
    }

    private static void CreateVsi(string dir, string baseName)
    {
        File.WriteAllText(Path.Combine(dir, baseName + ".vsi"), "");
        Directory.CreateDirectory(Path.Combine(dir, "_" + baseName + "_"));
    }

    [Fact]
    public async Task DeleteAfterImport_Removes_Vsi_And_Companion_Folder()
    {
        var dir = NewTempDir();
        CreateVsi(dir, "slide");
        var vsi = Path.Combine(dir, "slide.vsi");
        var companion = Path.Combine(dir, "_slide_");
        File.WriteAllText(Path.Combine(companion, "data.bin"), "x");

        var handler = CreateHandler(CreateMediator());
        var resp = await handler.Handle(new WsiImportCommand(dir, Guid.NewGuid(), null, DeleteAfterImport: true), default);

        resp.Imported.Should().Be(1);
        File.Exists(vsi).Should().BeFalse();
        Directory.Exists(companion).Should().BeFalse();
    }

    [Fact]
    public async Task DirectoryImport_With_MissingCompanion_Reports_Correct_ImportedCount()
    {
        var dir = NewTempDir();
        CreateVsi(dir, "a");
        File.WriteAllText(Path.Combine(dir, "b.vsi"), "");

        var handler = CreateHandler(CreateMediator());
        var resp = await handler.Handle(new WsiImportCommand(dir, Guid.NewGuid(), null), default);

        resp.Imported.Should().Be(1);
        resp.Errors.Should().ContainSingle();
    }

    [Fact]
    public async Task DirectoryImport_All_MissingCompanions_Reports_Zero_Not_Negative()
    {
        var dir = NewTempDir();
        File.WriteAllText(Path.Combine(dir, "a.vsi"), "");
        File.WriteAllText(Path.Combine(dir, "b.vsi"), "");
        File.WriteAllText(Path.Combine(dir, "c.vsi"), "");

        var handler = CreateHandler(CreateMediator());
        var resp = await handler.Handle(new WsiImportCommand(dir, Guid.NewGuid(), null), default);

        resp.Imported.Should().Be(0);
        resp.Errors.Should().HaveCount(3);
    }

    [Fact]
    public async Task DeleteAfterImport_When_CompanionDelete_Fails_Still_Counts_Import()
    {
        var dir = NewTempDir();
        CreateVsi(dir, "slide");
        var companion = Path.Combine(dir, "_slide_");
        var locked = Path.Combine(companion, "data.bin");
        File.WriteAllText(locked, "x");

        var handler = CreateHandler(CreateMediator());

        WsiImportResponse resp;
        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            resp = await handler.Handle(new WsiImportCommand(dir, Guid.NewGuid(), null, DeleteAfterImport: true), default);
        }

        resp.Imported.Should().Be(1);
        resp.Errors.Should().Contain(e => e.Contains("delete", StringComparison.OrdinalIgnoreCase));
    }
}
