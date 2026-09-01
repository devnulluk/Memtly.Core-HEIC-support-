using System.Reflection;
using Memtly.Core.Constants;
using Memtly.Core.Enums;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Png;
using MetadataExtractor.Formats.Xmp;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Localization;
using SkiaSharp;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace Memtly.Core.Helpers
{
    public interface IImageHelper
    {
        Task<bool> GenerateThumbnail(string filePath, string savePath, int size = 720);
        MediaType GuessMediaTypeFromExtension(string path);
        MediaType GetMediaType(string filePath);
        ImageOrientation GetOrientation(string path);
        DateTime? GetExifCreationDateTaken(string path);
        Task<bool> DownloadFFMPEG(string path);
    }

    public class ImageHelper : IImageHelper
    {
        private readonly IFileHelper _fileHelper;
        private readonly ILogger _logger;
        private readonly IStringLocalizer<Localization.Translations> _localizer;
        private static bool FfmpegInstalled = false;

        public ImageHelper(IFileHelper fileHelper, ILogger<ImageHelper> logger, IStringLocalizer<Localization.Translations> localizer)
        {
            _fileHelper = fileHelper;
            _logger = logger;
            _localizer = localizer;
        }

        public async Task<bool> GenerateThumbnail(string filePath, string savePath, int resolution = 720)
        {
            if (!_fileHelper.FileExists(filePath)) return false;

            var originalPath = filePath;
            string? temporaryDecodedPath = null;
            var mediaType = MediaType.Unknown;

            try
            {
                mediaType = GetMediaType(originalPath);
                if (mediaType == MediaType.Unknown)
                    mediaType = GuessMediaTypeFromExtension(originalPath);

                if (mediaType != MediaType.Image && mediaType != MediaType.Video)
                    return true;

                if (mediaType == MediaType.Video)
                {
                    if (!FfmpegInstalled)
                    {
                        _logger.LogWarning(_localizer["FFMPEG_Downloading"].Value);
                        return false;
                    }

                    try
                    {
                        var conversion = await FFmpeg.Conversions.FromSnippet.Snapshot(originalPath, savePath, TimeSpan.Zero);
                        await conversion.Start();
                        filePath = savePath;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to grab thumbnail frame from video - '{originalPath}' - {ex.Message}");
                        File.Copy(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!, Directories.Private.Assets, "BrokenVideo.webp"), savePath, true);
                        return false;
                    }
                }
                else if (HeifHelper.IsHeif(originalPath))
                {
                    temporaryDecodedPath = await HeifHelper.DecodeToTemporaryPng(originalPath);
                    if (temporaryDecodedPath == null)
                        throw new InvalidOperationException("libheif could not decode the HEIC/HEIF image.");
                    filePath = temporaryDecodedPath;
                }

                using var input = File.OpenRead(filePath);
                using var codec = SKCodec.Create(input, out _) ?? throw new InvalidOperationException("Unsupported or invalid image.");
                using var rawBitmap = new SKBitmap(codec.Info);
                var decodeResult = codec.GetPixels(codec.Info, rawBitmap.GetPixels());
                if (decodeResult != SKCodecResult.Success)
                    throw new InvalidOperationException($"Pixel decode failed: {decodeResult}");
                using var rawImage = SKImage.FromBitmap(rawBitmap);

                var (originWidth, originHeight) = GetDimensions(codec);
                SKImage correctedImage = rawImage;
                SKSurface? orientationSurface = null;
                if (codec.EncodedOrigin != SKEncodedOrigin.TopLeft)
                {
                    orientationSurface = SKSurface.Create(new SKImageInfo(originWidth, originHeight));
                    var c = orientationSurface.Canvas;
                    c.Clear(SKColors.Transparent);
                    switch (codec.EncodedOrigin)
                    {
                        case SKEncodedOrigin.TopRight: c.Translate(originWidth, 0); c.Scale(-1, 1); break;
                        case SKEncodedOrigin.BottomRight: c.Translate(originWidth, originHeight); c.RotateDegrees(180); break;
                        case SKEncodedOrigin.BottomLeft: c.Translate(0, originHeight); c.Scale(1, -1); break;
                        case SKEncodedOrigin.LeftTop: c.RotateDegrees(90); c.Scale(1, -1); break;
                        case SKEncodedOrigin.RightTop: c.Translate(originWidth, 0); c.RotateDegrees(90); break;
                        case SKEncodedOrigin.RightBottom: c.Translate(originWidth, originHeight); c.RotateDegrees(90); c.Scale(-1, 1); break;
                        case SKEncodedOrigin.LeftBottom: c.Translate(0, originHeight); c.RotateDegrees(-90); break;
                    }
                    c.DrawImage(rawImage, 0, 0, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
                    correctedImage = orientationSurface.Snapshot();
                }

                await EncodeScaledWebp(correctedImage, originWidth, originHeight, savePath, resolution, 80);

                // HEIC/HEIF masters remain untouched. A larger WebP beside the thumbnail is
                // used by browsers for the lightbox/full-screen view.
                if (HeifHelper.IsHeif(originalPath))
                {
                    var displayPath = Path.Combine(Path.GetDirectoryName(savePath)!, $"{Path.GetFileNameWithoutExtension(savePath)}.display.webp");
                    await EncodeScaledWebp(correctedImage, originWidth, originHeight, displayPath, 3840, 95, onlyDownscale: true);
                }

                if (!ReferenceEquals(correctedImage, rawImage)) correctedImage.Dispose();
                orientationSurface?.Dispose();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to generate thumbnail - '{originalPath}'");
                try
                {
                    File.Copy(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!, Directories.Private.Assets, mediaType == MediaType.Video ? "BrokenVideo.webp" : "BrokenImage.webp"), savePath, true);
                }
                catch { }
                return false;
            }
            finally
            {
                HeifHelper.TryDelete(temporaryDecodedPath);
            }
        }

        private static async Task EncodeScaledWebp(SKImage image, int sourceWidth, int sourceHeight, string savePath, int maxDimension, int quality, bool onlyDownscale = false)
        {
            var longest = Math.Max(sourceWidth, sourceHeight);
            var scale = longest > 0 ? (float)maxDimension / longest : 1f;
            if (onlyDownscale && scale > 1f) scale = 1f;
            var width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
            var height = Math.Max(1, (int)Math.Round(sourceHeight * scale));

            Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
            using var surface = SKSurface.Create(new SKImageInfo(width, height));
            surface.Canvas.Clear(SKColors.Transparent);
            surface.Canvas.DrawImage(image, new SKRect(0, 0, width, height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
            using var outputImage = surface.Snapshot();
            using var data = outputImage.Encode(SKEncodedImageFormat.Webp, quality);
            await using var output = File.Create(savePath);
            data.SaveTo(output);
            await output.FlushAsync();
        }

        public MediaType GuessMediaTypeFromExtension(string path)
        {
            try
            {
                var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg", ".tiff", ".tif", ".ico", ".heic", ".heif", ".avif" };
                var videoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mov", ".avi", ".wmv", ".flv", ".mkv", ".webm", ".m4v", ".mpg", ".mpeg", ".3gp", ".ts" };
                var extension = $".{Path.GetExtension(path).Trim('.')}";
                if (imageExtensions.Contains(extension)) return MediaType.Image;
                if (videoExtensions.Contains(extension)) return MediaType.Video;
            }
            catch { }
            return MediaType.Unknown;
        }

        public MediaType GetMediaType(string path)
        {
            try
            {
                var provider = new FileExtensionContentTypeProvider();
                if (provider.TryGetContentType(path, out string? contentType))
                {
                    if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return MediaType.Image;
                    if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)) return MediaType.Video;
                }
            }
            catch { }
            return GuessMediaTypeFromExtension(path);
        }

        public ImageOrientation GetOrientation(string path)
        {
            if (!_fileHelper.FileExists(path)) return ImageOrientation.Unknown;
            string? decoded = null;
            try
            {
                var decodePath = path;
                if (HeifHelper.IsHeif(path))
                {
                    decoded = HeifHelper.DecodeToTemporaryPng(path).GetAwaiter().GetResult();
                    if (decoded == null) return ImageOrientation.Unknown;
                    decodePath = decoded;
                }
                using var input = File.OpenRead(decodePath);
                using var codec = SKCodec.Create(input) ?? throw new InvalidOperationException("Unsupported or invalid image.");
                return GetOrientation(codec);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to get image orientation - '{path}'");
                return ImageOrientation.Unknown;
            }
            finally { HeifHelper.TryDelete(decoded); }
        }

        public ImageOrientation GetOrientation(SKCodec codec)
        {
            var (width, height) = GetDimensions(codec);
            return width > height ? ImageOrientation.Landscape : width < height ? ImageOrientation.Portrait : ImageOrientation.Square;
        }

        public (int, int) GetDimensions(SKCodec codec)
        {
            var rotated = IsRotated(codec);
            return (rotated ? codec.Info.Height : codec.Info.Width, rotated ? codec.Info.Width : codec.Info.Height);
        }

        public bool IsRotated(SKCodec codec) => codec.EncodedOrigin == SKEncodedOrigin.LeftTop || codec.EncodedOrigin == SKEncodedOrigin.RightTop || codec.EncodedOrigin == SKEncodedOrigin.LeftBottom || codec.EncodedOrigin == SKEncodedOrigin.RightBottom;

        public DateTime? GetExifCreationDateTaken(string path)
        {
            try
            {
                var directories = ImageMetadataReader.ReadMetadata(path);
                var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                if (subIfd?.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var original) == true) return original;
                if (subIfd?.TryGetDateTime(ExifDirectoryBase.TagDateTimeDigitized, out var digitized) == true) return digitized;
                var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
                if (ifd0?.TryGetDateTime(ExifDirectoryBase.TagDateTime, out var dateTime) == true) return dateTime;
                var png = directories.OfType<PngDirectory>().FirstOrDefault();
                if (png?.TryGetDateTime(PngDirectory.TagLastModificationTime, out var pngDate) == true) return pngDate;
                var xmp = directories.OfType<XmpDirectory>().FirstOrDefault();
                var xmpDate = xmp?.XmpMeta?.GetPropertyString("http://ns.adobe.com/xap/1.0/", "xmp:CreateDate");
                if (!string.IsNullOrEmpty(xmpDate) && DateTime.TryParse(xmpDate, out var parsed)) return parsed;
            }
            catch (Exception ex) { _logger.LogWarning(ex, $"Failed to get image EXIF creation datetime - '{path}'"); }
            return null;
        }

        public async Task<bool> DownloadFFMPEG(string path)
        {
            try
            {
                if (!_fileHelper.DirectoryExists(path))
                {
                    _fileHelper.CreateDirectoryIfNotExists(path);
                    await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, path);
                }
                FFmpeg.SetExecutablesPath(path);
                FfmpegInstalled = true;
                return true;
            }
            catch (Exception ex) { _logger.LogWarning(ex, $"Failed to download FFmpeg - '{path}'"); }
            return false;
        }
    }
}
