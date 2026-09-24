using Transom.Core.Models;
using Transom.Core.Providers;

namespace Transom.App.Tests.TestDoubles;

internal sealed class FakeBlogProvider : IBlogProvider
{
    public string Id => "fake";

    public Func<PostDraft, CancellationToken, Task<PublishResult>>? OnPublish { get; set; }

    public PostDraft? LastDraft { get; private set; }

    public Task<IReadOnlyList<BlogInfo>> GetBlogsAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<BlogInfo>>([]);

    public Task<PublishResult> PublishAsync(PostDraft draft, CancellationToken cancellationToken)
    {
        LastDraft = draft;
        return OnPublish?.Invoke(draft, cancellationToken)
            ?? Task.FromResult(new PublishResult("https://example.micro.blog/post.html", null));
    }
}