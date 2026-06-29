using Avalonia;
using VsiConverter.UI.Models;
using VsiConverter.UI.Services;

namespace VsiConverter.UI;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0].EndsWith(".vsi", StringComparison.OrdinalIgnoreCase))
        {
            RunHeadlessAsync(Path.GetFullPath(args[0])).GetAwaiter().GetResult();
            return;
        }
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static async Task RunHeadlessAsync(string vsiPath)
    {
        Console.WriteLine("=== VSI \u2192 DZI Headless Converter ===");
        Console.WriteLine($"File: {vsiPath}\n");

        if (!File.Exists(vsiPath))
        {
            Console.WriteLine("ERROR: File not found");
            Environment.Exit(1);
        }

        // 1. Detect tools
        Console.Write("Detecting tools... ");
        var status = await ToolchainManager.DetectAllAsync();
        if (!status.JavaFound || !status.BfconvertFound || !status.VipsFound)
        {
            Console.WriteLine("FAILED");
            if (!status.JavaFound) Console.WriteLine("  Java not found");
            if (!status.BfconvertFound) Console.WriteLine("  bfconvert not found");
            if (!status.VipsFound) Console.WriteLine("  vips not found");
            Environment.Exit(1);
        }
        Console.WriteLine("OK");
        Console.WriteLine($"  Java: {status.JavaVersion}");
        Console.WriteLine($"  bfconvert: {status.BfconvertPath}");
        Console.WriteLine($"  vips: {status.VipsPath}\n");

        // 2. Detect series
        Console.Write("Detecting image series... ");
        var series = await SeriesDetector.DetectSeriesAsync(vsiPath);
        var bestIndex = 0;
        if (series.Count == 0)
        {
            Console.WriteLine("NONE (using index 0)");
        }
        else
        {
            Console.WriteLine($"{series.Count} found");
            foreach (var s in series)
                Console.WriteLine($"  Series {s.Index}: {s.Width}x{s.Height}");
            bestIndex = series.MaxBy(s => s.Width * s.Height)!.Index;
            Console.WriteLine($"  \u2192 Using series {bestIndex}\n");
        }

        // 3. Run pipeline
        Console.WriteLine("Starting conversion... (Ctrl+C to cancel)\n");

        var runner = new PipelineRunner();
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, _) => { Console.WriteLine("\nCancelling..."); cts.Cancel(); };

        // ReadStderrLinesAsync already echoes every tool line with [bfconvert]/[vips]
        var progress = new Progress<ConversionProgress>(_ => { });

        var startTime = DateTime.UtcNow;
        var result = await runner.RunAsync(vsiPath, bestIndex, 90, progress, cts.Token);
        var elapsed = DateTime.UtcNow - startTime;

        Console.WriteLine();
        if (result.Success)
        {
            Console.WriteLine($"Completed in {elapsed.TotalMinutes:F1} min");
            Console.WriteLine($"Output: {result.OutputPath}");
        }
        else if (result.IsCancelled)
        {
            Console.WriteLine("Cancelled");
        }
        else
        {
            Console.WriteLine($"Failed: {result.ErrorMessage}");
        }
    }

    static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}
