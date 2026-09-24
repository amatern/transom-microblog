using System.Runtime.InteropServices.WindowsRuntime;

using Transom.Core.Media;

using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Transom.App.Services;

/// <summary>The one WinRT-touching piece of M2 (CLAUDE.md Rule 5: <c>Transom.Core</c> can't
/// reference <c>Windows.*</c>). SPEC.md §7: resize the long edge to ≤2048px, re-encode JPEG q=85
/// (which strips EXIF/GPS), keep PNG unless the re-encoded file would be over 5MB, and pass an
/// animated GIF through untouched.</summary>
public sealed class WicImageProcessor : IImageProcessor
{
    public async Task<ProcessedImage> ProcessAsync(Stream source, string contentType, ImageProcessingOptions options, CancellationToken cancellationToken)
    {
        if (ImageContentTypes.IsAnimatedGifPassthrough(contentType))
        {
            var passthrough = new MemoryStream();
            await source.CopyToAsync(passthrough, cancellationToken).ConfigureAwait(false);
            passthrough.Position = 0;
            return new ProcessedImage(passthrough, ImageContentTypes.Gif, "image.gif");
        }

        using var sourceRandomAccessStream = source.AsRandomAccessStream();
        BitmapDecoder decoder;
        try
        {
            decoder = await BitmapDecoder.CreateAsync(sourceRandomAccessStream).AsTask(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Couldn't read this image — it may be corrupt or an unsupported format.", ex);
        }

        var (targetWidth, targetHeight) = ImageResizeMath.ComputeTargetSize((int)decoder.PixelWidth, (int)decoder.PixelHeight, options.MaxDimension);

        if (contentType == ImageContentTypes.Png)
        {
            var png = await EncodeAsync(decoder, BitmapEncoder.PngEncoderId, targetWidth, targetHeight, cancellationToken).ConfigureAwait(false);
            if (png.Length <= options.MaxPngBytes)
            {
                return new ProcessedImage(png, ImageContentTypes.Png, "image.png");
            }

            png.Dispose();
        }

        var jpeg = await EncodeAsync(decoder, BitmapEncoder.JpegEncoderId, targetWidth, targetHeight, cancellationToken, options.JpegQuality).ConfigureAwait(false);
        return new ProcessedImage(jpeg, ImageContentTypes.Jpeg, "image.jpg");
    }

    private static async Task<MemoryStream> EncodeAsync(BitmapDecoder decoder, Guid encoderId, uint targetWidth, uint targetHeight, CancellationToken cancellationToken, int? jpegQuality = null)
        => await EncodeAsync(decoder, encoderId, (int)targetWidth, (int)targetHeight, cancellationToken, jpegQuality).ConfigureAwait(false);

    private static async Task<MemoryStream> EncodeAsync(BitmapDecoder decoder, Guid encoderId, int targetWidth, int targetHeight, CancellationToken cancellationToken, int? jpegQuality = null)
    {
        var softwareBitmap = await decoder.GetSoftwareBitmapAsync().AsTask(cancellationToken).ConfigureAwait(false);
        using var outputStream = new InMemoryRandomAccessStream();

        BitmapPropertySet? propertySet = null;
        if (jpegQuality is { } quality)
        {
            propertySet = new BitmapPropertySet
            {
                ["ImageQuality"] = new BitmapTypedValue(quality / 100.0, Windows.Foundation.PropertyType.Single),
            };
        }

        var encoder = propertySet is null
            ? await BitmapEncoder.CreateAsync(encoderId, outputStream).AsTask(cancellationToken).ConfigureAwait(false)
            : await BitmapEncoder.CreateAsync(encoderId, outputStream, propertySet).AsTask(cancellationToken).ConfigureAwait(false);

        encoder.SetSoftwareBitmap(softwareBitmap);
        encoder.BitmapTransform.ScaledWidth = (uint)targetWidth;
        encoder.BitmapTransform.ScaledHeight = (uint)targetHeight;
        encoder.IsThumbnailGenerated = false;
        await encoder.FlushAsync().AsTask(cancellationToken).ConfigureAwait(false);

        var result = new MemoryStream();
        outputStream.Seek(0);
        await outputStream.AsStreamForRead().CopyToAsync(result, cancellationToken).ConfigureAwait(false);
        result.Position = 0;
        return result;
    }
}