using Transom.Core.Media;

namespace Transom.Core.Tests.Media;

public class ImageResizeMathTests
{
    [Fact]
    public void ComputeTargetSize_AlreadySmallerThanMax_ReturnsOriginal()
    {
        var (width, height) = ImageResizeMath.ComputeTargetSize(800, 600, maxDimension: 2048);

        Assert.Equal(800, width);
        Assert.Equal(600, height);
    }

    [Fact]
    public void ComputeTargetSize_ExactlyAtMax_ReturnsOriginal()
    {
        var (width, height) = ImageResizeMath.ComputeTargetSize(2048, 1024, maxDimension: 2048);

        Assert.Equal(2048, width);
        Assert.Equal(1024, height);
    }

    [Fact]
    public void ComputeTargetSize_WideImage_ScalesDownPreservingAspectRatio()
    {
        var (width, height) = ImageResizeMath.ComputeTargetSize(4096, 2048, maxDimension: 2048);

        Assert.Equal(2048, width);
        Assert.Equal(1024, height);
    }

    [Fact]
    public void ComputeTargetSize_TallImage_ScalesDownPreservingAspectRatio()
    {
        var (width, height) = ImageResizeMath.ComputeTargetSize(1000, 4000, maxDimension: 2048);

        Assert.Equal(512, width);
        Assert.Equal(2048, height);
    }

    [Fact]
    public void ComputeTargetSize_NeverUpscales()
    {
        var (width, height) = ImageResizeMath.ComputeTargetSize(100, 50, maxDimension: 2048);

        Assert.Equal(100, width);
        Assert.Equal(50, height);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(-1, 100)]
    public void ComputeTargetSize_DegenerateInput_ReturnsOriginalRatherThanThrowing(int width, int height)
    {
        var result = ImageResizeMath.ComputeTargetSize(width, height, maxDimension: 2048);

        Assert.Equal((width, height), result);
    }
}
