using Memtly.Core.Enums;
using Memtly.Core.Helpers;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Memtly.Core.UnitTests.Tests.Helpers
{
    public class ImageHelperTests
    {
        private readonly IFileHelper _fileHelper = Substitute.For<IFileHelper>();
        private readonly ILogger<ImageHelper> _logger = Substitute.For<ILogger<ImageHelper>>();
        private readonly IStringLocalizer<Memtly.Localization.Translations> _localizer = Substitute.For<IStringLocalizer<Memtly.Localization.Translations>>();
        private readonly IDictionary<ImageOrientation, SKCodec?> _imageCollection;

        public ImageHelperTests()
        {
            _imageCollection = new Dictionary<ImageOrientation, SKCodec?>()
            {
                { ImageOrientation.Square, CreateMockImage(100, 100) },
                { ImageOrientation.Landscape, CreateMockImage(200, 100) },
                { ImageOrientation.Portrait, CreateMockImage(100, 200) }
            };
        }

        [SetUp]
        public void Setup()
        {
        }

        [TestCase(ImageOrientation.Square)]
        [TestCase(ImageOrientation.Landscape)]
        [TestCase(ImageOrientation.Portrait)]
        public void ImageHelper_GetOrientation(ImageOrientation orientation)
        {
            var image = _imageCollection[orientation];
            Assert.IsNotNull(image);

            var actual = new ImageHelper(_fileHelper, _logger, _localizer).GetOrientation(image);
            Assert.That(actual, Is.EqualTo(orientation));
        }

        private SKCodec CreateMockImage(int width, int height)
        {
            var info = new SKImageInfo(width, height);

            using var surface = SKSurface.Create(info);
            surface.Canvas.Clear(SKColors.White);

            using var image = surface.Snapshot();
            using var encodedData = image.Encode(SKEncodedImageFormat.Png, 100);

            var stream = new MemoryStream(encodedData.ToArray());
            return SKCodec.Create(stream);
        }
    }
}