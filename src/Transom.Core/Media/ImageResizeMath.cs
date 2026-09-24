namespace Transom.Core.Media;

/// <summary>Pure long-edge-scaling math for SPEC.md §7 ("resize so the long edge ≤ 2048 px"),
/// isolated from <c>WicImageProcessor</c>'s WinRT decode/encode so it's unit-testable on any OS.</summary>
public static class ImageResizeMath
{
    public static (int Width, int Height) ComputeTargetSize(int originalWidth, int originalHeight, int maxDimension)
    {
        if (originalWidth <= 0 || originalHeight <= 0 || maxDimension <= 0)
        {
            return (originalWidth, originalHeight);
        }

        var longEdge = Math.Max(originalWidth, originalHeight);
        if (longEdge <= maxDimension)
        {
            return (originalWidth, originalHeight);
        }

        var scale = (double)maxDimension / longEdge;
        var width = Math.Max(1, (int)Math.Round(originalWidth * scale));
        var height = Math.Max(1, (int)Math.Round(originalHeight * scale));
        return (width, height);
    }
}
