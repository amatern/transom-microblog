namespace Transom.Core.Models;

/// <summary>An already-uploaded image attached to a post: the URL <c>UploadMediaAsync</c>
/// returned, and the user's alt text. SPEC.md §7: empty alt text is allowed and never blocks
/// publish, so it stays nullable all the way to the wire (an empty <c>mp-photo-alt</c> value).</summary>
public sealed record DraftImage(string Url, string? AltText);