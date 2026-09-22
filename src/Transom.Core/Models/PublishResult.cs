namespace Transom.Core.Models;

/// <summary>Result of a successful publish: the post's URL, and a preview URL for drafts.</summary>
public sealed record PublishResult(string Url, string? PreviewUrl);