using Transom.Core.Models;
using Transom.Core.Providers;

namespace Transom.App.Tests.TestDoubles;

internal sealed class FakeBlogProvider : IBlogProvider
{
    public string Id => "fake";

    public Func<PostDraft, CancellationToken, Task<PublishResult>>? OnPublish { get; set; }

    public Func<string, CancellationToken, Task<MediaItem>>? OnUploadMedia { get; set; }

    public PostDraft? LastDraft { get; private set; }

    public Task<IReadOnlyList<BlogInfo>> GetBlogsAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<BlogInfo>>([]);

    public Task<MediaItem> UploadMediaAsync(Stream data, string fileName, string contentType, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(1.0);
        return OnUploadMedia?.Invoke(fileName, cancellationToken)
            ?? Task.FromResult(new MediaItem($"https://cdn.micro.blog/uploads/{fileName}"));
    }

    public Task<PublishResult> PublishAsync(PostDraft draft, CancellationToken cancellationToken)
    {
        LastDraft = draft;
        return OnPublish?.Invoke(draft, cancellationToken)
            ?? Task.FromResult(new PublishResult("https://example.micro.blog/post.html", null));
    }
}