namespace Transom.Core.Models;

/// <summary>A post ready to publish. <paramref name="PostAsDraft"/> maps to <c>post-status=draft</c>
/// (SPEC §6.2). <paramref name="Images"/> defaults to empty so every existing call site
/// (text-only posts) is unaffected — a null argument is coalesced the same way, not left null.</summary>
public sealed record PostDraft(string Content, string? Title, bool PostAsDraft, IReadOnlyList<DraftImage>? Images = null)
{
    public IReadOnlyList<DraftImage> Images { get; init; } = Images ?? Array.Empty<DraftImage>();
}