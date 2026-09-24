namespace Transom.Core.Media;

/// <summary>The image types SPEC.md §7 lists (JPEG, PNG, GIF, WebP, HEIC), and the file-extension
/// mapping the composer uses to pick a content type for a picked/dropped file.</summary>
public static class ImageContentTypes
{
    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";
    public const string Gif = "image/gif";
    public const string WebP = "image/webp";
    public const string Heic = "image/heic";

    /// <summary>SPEC.md §7: "animated GIF uploaded untouched" — <c>WicImageProcessor</c> checks
    /// this before attempting to decode/resize/re-encode at all.</summary>
    public static bool IsAnimatedGifPassthrough(string contentType) =>
        string.Equals(contentType, Gif, StringComparison.OrdinalIgnoreCase);

    public static string? FromFileExtension(string extension) => extension.TrimStart('.').ToLowerInvariant() switch
    {
        "jpg" or "jpeg" => Jpeg,
        "png" => Png,
        "gif" => Gif,
        "webp" => WebP,
        "heic" or "heif" => Heic,
        _ => null,
    };
}
