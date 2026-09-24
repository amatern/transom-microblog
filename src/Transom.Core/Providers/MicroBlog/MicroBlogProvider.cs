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
    private string? _cachedMediaEndpoint;

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

    public async Task<MediaItem> UploadMediaAsync(Stream data, string fileName, string contentType, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var token = RequireToken();
        var mediaEndpoint = await GetMediaEndpointAsync(token, cancellationToken).ConfigureAwait(false);
        return await _micropubClient.UploadMediaAsync(token, mediaEndpoint, data, fileName, contentType, progress, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> GetMediaEndpointAsync(string token, CancellationToken cancellationToken)
    {
        if (_cachedMediaEndpoint is { } cached)
        {
            return cached;
        }

        var config = await _micropubClient.GetConfigAsync(token, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(config.MediaEndpoint))
        {
            throw new MicropubException(null, "This Micro.blog account has no media endpoint configured.");
        }

        _cachedMediaEndpoint = config.MediaEndpoint;
        return _cachedMediaEndpoint;
    }

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
