namespace Transom.Core.Media;

/// <summary>Resizes and re-encodes an image per SPEC.md §7. The only implementation
/// (<c>WicImageProcessor</c>, <c>Transom.App/Services</c>) uses <c>Windows.Graphics.Imaging</c>, a
/// WinRT API Core can't reference (CLAUDE.md: Core builds and tests on Linux) — this interface is
/// what lets the composer depend on image processing without depending on WinRT.</summary>
public interface IImageProcessor
{
    Task<ProcessedImage> ProcessAsync(Stream source, string contentType, ImageProcessingOptions options, CancellationToken cancellationToken);
}

/// <summary>SPEC.md §7's processing rules. <see cref="Default"/> is what the composer uses; the
/// individual values exist as fields so Settings (SPEC.md §4.4: "image settings — max dimension,
/// JPEG quality, strip metadata") can override them later without a new type.</summary>
public sealed record ImageProcessingOptions(int MaxDimension, int JpegQuality, long MaxPngBytes)
{
    public static ImageProcessingOptions Default { get; } = new(MaxDimension: 2048, JpegQuality: 85, MaxPngBytes: 5 * 1024 * 1024);
}

/// <summary>The result of processing one image: the encoded bytes, its final content type (which
/// can differ from the input — e.g. an oversized PNG becomes JPEG), and a file name with the
/// matching extension for the multipart upload.</summary>
public sealed record ProcessedImage(Stream Content, string ContentType, string FileName);
