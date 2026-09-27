using Transom.Core.Models;

namespace Transom.Core.Providers;

/// <summary>
/// A blogging platform Transom can publish to. This is a subset of SPEC.md §5.2's target
/// interface — <c>VerifyAsync</c>, <c>GetCategoriesAsync</c>, <c>GetRemoteDraftsAsync</c>,
/// <c>Capabilities</c> and <c>Limits</c> are added by the milestones that need them (M5
/// categories/drafts/multi-blog UI). Implementations read the current token themselves (e.g. from
/// <see cref="Transom.Core.Credentials.ICredentialStore"/>) — no method here takes one, so nothing
/// outside <c>Providers/&lt;name&gt;/</c> ever handles a raw token (CLAUDE.md Rule 3).
/// </summary>
public interface IBlogProvider
{
    string Id { get; }

    Task<IReadOnlyList<BlogInfo>> GetBlogsAsync(CancellationToken cancellationToken);

    Task<MediaItem> UploadMediaAsync(Stream data, string fileName, string contentType, IProgress<double>? progress, CancellationToken cancellationToken);

    Task<PublishResult> PublishAsync(PostDraft draft, CancellationToken cancellationToken);
}