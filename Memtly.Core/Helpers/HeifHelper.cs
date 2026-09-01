using System.Diagnostics;

namespace Memtly.Core.Helpers
{
    /// <summary>
    /// Decodes HEIC/HEIF masters to a temporary PNG for the existing SkiaSharp
    /// processing pipeline. The source file is opened read-only by heif-convert
    /// and is never replaced or modified.
    /// </summary>
    internal static class HeifHelper
    {
        private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".heic", ".heif"
        };

        public static bool IsHeif(string path) => Extensions.Contains(Path.GetExtension(path));

        public static async Task<string?> DecodeToTemporaryPng(string sourcePath)
        {
            if (!IsHeif(sourcePath))
            {
                return null;
            }

            var tempPath = Path.Combine(Path.GetTempPath(), $"memtly-heif-{Guid.NewGuid():N}.png");
            var startInfo = new ProcessStartInfo
            {
                FileName = "heif-convert",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add(sourcePath);
            startInfo.ArgumentList.Add(tempPath);

            try
            {
                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return null;
                }

                await process.WaitForExitAsync();
                if (process.ExitCode != 0 || !File.Exists(tempPath) || new FileInfo(tempPath).Length == 0)
                {
                    TryDelete(tempPath);
                    return null;
                }

                return tempPath;
            }
            catch
            {
                TryDelete(tempPath);
                return null;
            }
        }

        public static void TryDelete(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }
    }
}
