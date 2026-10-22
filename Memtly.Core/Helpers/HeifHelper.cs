using System.Diagnostics;

namespace Memtly.Core.Helpers;

/// <summary>Read-only HEIF decoding. Temporary derivatives never replace originals.</summary>
internal static class HeifHelper
{
    private static readonly SemaphoreSlim Slots = new(2, 2);
    public static bool IsHeif(string path) =>
        Path.GetExtension(path).Equals(".heic", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(path).Equals(".heif", StringComparison.OrdinalIgnoreCase);

    public static async Task<string?> DecodeToTemporaryPng(string sourcePath)
    {
        if (!IsHeif(sourcePath) || !File.Exists(sourcePath)) return null;
        if (!await Slots.WaitAsync(TimeSpan.FromSeconds(30))) return null;
        var directory = Path.Combine(Path.GetTempPath(), "memtly-heif-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(directory, "primary.png");
        var keep = false;
        try
        {
            Directory.CreateDirectory(directory);
            var start = new ProcessStartInfo("heif-convert")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            // Absolute paths cannot be interpreted as command-line switches.
            start.ArgumentList.Add(Path.GetFullPath(sourcePath));
            start.ArgumentList.Add(output);
            using var process = Process.Start(start);
            if (process == null) return null;
            // Drain both streams concurrently: waiting first can deadlock on a full pipe.
            var stdout = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);
            var stderr = process.StandardError.BaseStream.CopyToAsync(Stream.Null);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
                await Task.WhenAll(stdout, stderr).WaitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); } catch { }
                return null;
            }
            if (process.ExitCode != 0) return null;
            // Some versions number the output even for a single top-level image.
            // Never silently pick an arbitrary still from a multi-image container.
            if (!File.Exists(output))
            {
                var candidates = Directory.GetFiles(directory, "*.png");
                if (candidates.Length != 1) return null;
                File.Move(candidates[0], output);
            }
            var length = new FileInfo(output).Length;
            if (length == 0 || length > 512L * 1024 * 1024) return null;
            keep = true;
            return output;
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or InvalidOperationException or UnauthorizedAccessException)
        {
            return null;
        }
        finally
        {
            if (!keep) TryDelete(output);
            Slots.Release();
        }
    }

    public static void TryDelete(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            // Cleanup is restricted to a directory that this helper could have created.
            if (directory == null || Path.GetDirectoryName(directory) != Path.TrimEndingDirectorySeparator(Path.GetTempPath()) ||
                !Path.GetFileName(directory).StartsWith("memtly-heif-", StringComparison.Ordinal)) return;
            var suffix = Path.GetFileName(directory)["memtly-heif-".Length..];
            if (Guid.TryParseExact(suffix, "N", out _) && Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
