using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;

namespace VsiConverter.UI.Services;

public class ToolchainStatus
{
    public bool JavaFound { get; set; }
    public string? JavaVersion { get; set; }
    public bool BfconvertFound { get; set; }
    public string? BfconvertPath { get; set; }
    public bool VipsFound { get; set; }
    public string? VipsPath { get; set; }
}

public static class ToolchainManager
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(5) };

    public static string GetStorageDirectory()
    {
        var baseDir = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "VsiConverter", "bin")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VsiConverter", "bin");
        Directory.CreateDirectory(baseDir);
        return baseDir;
    }

    public static async Task<ToolchainStatus> DetectAllAsync()
    {
        var settings = SettingsStore.Load();
        var status = new ToolchainStatus();

        var javaPath = settings.JavaPath ?? "java";
        var javaResult = await RunDetectionAsync(javaPath, "-version");
        status.JavaFound = javaResult.Found;
        status.JavaVersion = javaResult.Version;

        var bfconvertPath = settings.BfconvertPath ?? FindTool("bfconvert");
        if (bfconvertPath is not null)
        {
            var bfResult = await RunDetectionAsync(bfconvertPath, "-version");
            status.BfconvertFound = bfResult.Found;
            status.BfconvertPath = bfResult.Found ? bfconvertPath : null;
        }

        var vipsPath = settings.VipsPath ?? FindTool("vips");
        if (vipsPath is not null)
        {
            var vipsResult = await RunDetectionAsync(vipsPath, "--version");
            status.VipsFound = vipsResult.Found;
            status.VipsPath = vipsResult.Found ? vipsPath : null;
        }

        return status;
    }

    public static string? FindTool(string name)
    {
        var storageDir = GetStorageDirectory();
        var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"{name}.exe" : name;
        var storagePath = Path.Combine(storageDir, exeName);
        if (File.Exists(storagePath)) return storagePath;

        // Check storageDir/bin/ (vips extracts to bin/vips.exe)
        var binPath = Path.Combine(storageDir, "bin", exeName);
        if (File.Exists(binPath)) return binPath;

        // For bfconvert, also check for .jar variant
        if (name == "bfconvert")
        {
            var jarPath = Path.Combine(storageDir, "bfconvert.jar");
            if (File.Exists(jarPath)) return jarPath;
        }

        // Check PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            var dirTrimmed = dir.Trim();
            if (string.IsNullOrEmpty(dirTrimmed)) continue;
            var fullPath = Path.Combine(dirTrimmed, exeName);
            if (File.Exists(fullPath)) return fullPath;
        }

        return null;
    }

    private static async Task<(bool Found, string? Version)> RunDetectionAsync(string command, string args)
    {
        try
        {
            var psi = new ProcessStartInfo(command, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process is null) return (false, null);

            var output = await process.StandardError.ReadToEndAsync();
            var output2 = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0) return (false, null);

            var combined = (output + output2).Trim();
            var version = combined.Split('\n', '\r')[0];
            return (true, version);
        }
        catch
        {
            return (false, null);
        }
    }

    public static async Task DownloadToolAsync(string toolName, IProgress<double> progress, CancellationToken ct)
    {
        var storageDir = GetStorageDirectory();

        switch (toolName)
        {
            case "bfconvert":
            {
                var zipPath = Path.Combine(Path.GetTempPath(), "bftools.zip");
                var url = "https://github.com/ome/bio-formats/releases/download/v7.3.0/bftools.zip";
                await DownloadFileAsync(url, zipPath, progress, ct);
                ZipFile.ExtractToDirectory(zipPath, storageDir, overwriteFiles: true);
                File.Delete(zipPath);
                break;
            }
            case "vips":
            {
                var zipPath = Path.Combine(Path.GetTempPath(), "vips.zip");
                string url;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = "https://github.com/libvips/libvips/releases/download/v8.15.2/vips-dev-w64-web-8.15.2.zip";
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    throw new PlatformNotSupportedException("On macOS, install vips via: brew install vips");
                }
                else
                {
                    throw new PlatformNotSupportedException("Unsupported platform");
                }
                await DownloadFileAsync(url, zipPath, progress, ct);
                ZipFile.ExtractToDirectory(zipPath, storageDir, overwriteFiles: true);
                // vips zip contains versioned subdirectory; move contents up
                var vipsDir = Directory.GetDirectories(storageDir, "vips-dev-*").FirstOrDefault();
                if (vipsDir is not null)
                {
                    foreach (var f in Directory.GetFiles(vipsDir))
                        File.Move(f, Path.Combine(storageDir, Path.GetFileName(f)), overwrite: true);
                    foreach (var d in Directory.GetDirectories(vipsDir))
                        Directory.Move(d, Path.Combine(storageDir, Path.GetFileName(d)));
                    Directory.Delete(vipsDir);
                }
                File.Delete(zipPath);
                break;
            }
            default:
                throw new ArgumentException($"Unknown tool: {toolName}");
        }
    }

    private static async Task DownloadFileAsync(string url, string destPath, IProgress<double> progress, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = File.Create(destPath);
        var buffer = new byte[8192];
        long bytesRead = 0;
        int read;
        while ((read = await contentStream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
            bytesRead += read;
            if (totalBytes > 0)
                progress.Report((double)bytesRead / totalBytes);
        }
    }
}
