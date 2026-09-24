using Transom.Core.Media;

namespace Transom.Core.Tests.Media;

public class ImageContentTypesTests
{
    [Theory]
    [InlineData("image/gif", true)]
    [InlineData("IMAGE/GIF", true)]
    [InlineData("image/jpeg", false)]
    [InlineData("image/png", false)]
    public void IsAnimatedGifPassthrough_MatchesGifCaseInsensitively(string contentType, bool expected)
    {
        Assert.Equal(expected, ImageContentTypes.IsAnimatedGifPassthrough(contentType));
    }

    [Theory]
    [InlineData(".jpg", "image/jpeg")]
    [InlineData(".jpeg", "image/jpeg")]
    [InlineData(".png", "image/png")]
    [InlineData(".gif", "image/gif")]
    [InlineData(".webp", "image/webp")]
    [InlineData(".heic", "image/heic")]
    [InlineData(".heif", "image/heic")]
    [InlineData(".JPG", "image/jpeg")]
    public void FromFileExtension_KnownExtensions_ReturnsContentType(string extension, string expected)
    {
        Assert.Equal(expected, ImageContentTypes.FromFileExtension(extension));
    }

    [Fact]
    public void FromFileExtension_UnknownExtension_ReturnsNull()
    {
        Assert.Null(ImageContentTypes.FromFileExtension(".bmp"));
    }
}