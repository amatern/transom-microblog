namespace Transom.Core.Models;

/// <summary>A post ready to publish. <paramref name="PostAsDraft"/> maps to <c>post-status=draft</c> (SPEC §6.2).</summary>
public sealed record PostDraft(string Content, string? Title, bool PostAsDraft);