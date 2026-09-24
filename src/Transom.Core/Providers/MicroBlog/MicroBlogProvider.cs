using System.Linq;

using Transom.Core.Credentials;
using Transom.Core.MicroBlog;
using Transom.Core.Models;

namespace Transom.Core.Providers.MicroBlog;

/// <summary>The Micro.blog <see cref="IBlogProvider"/>. Provider-specific — nothing outside this
/// folder may reference it directly; callers use <see cref="IBlogProvider"/> (CLAUDE.md Rule 3).</summary>
public sealed class MicroBlogProvider : IBlogProvider
{
    private readonly MicropubClient _micropubClient;
    private readonly ICredentialStore _credentialStore;
    private readonly string _accountId;

    public string Id => "microblog";

    public MicroBlogProvider(MicropubClient micropubClient, ICredentialStore credentialStore, string accountId)
    {
        _micropubClient = micropubClient;
        _credentialStore = credentialStore;
        _accountId = accountId;
    }

    public async Task<IReadOnlyList<BlogInfo>> GetBlogsAsync(CancellationToken cancellationToken)
    {
        var config = await _micropubClient.GetConfigAsync(RequireToken(), cancellationToken).ConfigureAwait(false);
        return WithDefaultMarked(config.Destinations);
    }

    /// <summary>SPEC.md §6.2: a real account's <c>q=config</c> flags its default blog via
    /// <c>microblog-default</c> on one destination, but that flag isn't guaranteed to be present —
    /// when no destination is flagged, the first one is treated as the default instead.</summary>
    private static IReadOnlyList<BlogInfo> WithDefaultMarked(IReadOnlyList<BlogInfo> destinations)
    {
        if (destinations.Count == 0 || destinations.Any(blog => blog.IsDefault))
        {
            return destinations;
        }

        var withDefault = destinations.ToList();
        withDefault[0] = withDefault[0] with { IsDefault = true };
        return withDefault;
    }

    public Task<PublishResult> PublishAsync(PostDraft draft, CancellationToken cancellationToken)
    {
        return _micropubClient.PublishAsync(RequireToken(), draft, cancellationToken);
    }

    private string RequireToken()
    {
        return _credentialStore.TryGet(_accountId)
            ?? throw new InvalidOperationException("No Micro.blog account is signed in.");
    }
}