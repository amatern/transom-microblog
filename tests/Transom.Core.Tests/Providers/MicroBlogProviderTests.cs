using System.Net;
using System.Text;

using Transom.Core.Credentials;
using Transom.Core.MicroBlog;
using Transom.Core.Models;
using Transom.Core.Providers.MicroBlog;
using Transom.Core.Tests.Fixtures;
using Transom.Core.Tests.Http;

namespace Transom.Core.Tests.Providers;

public class MicroBlogProviderTests
{
    private const string AccountId = "default";

    [Fact]
    public async Task GetBlogsAsync_ReturnsDestinationsFromConfig()
    {
        var handler = new FixtureHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(FixtureFile.ReadText("config-two-blogs.json"), Encoding.UTF8, "application/json"),
        });
        var provider = BuildProvider(handler, "test-token");

        var blogs = await provider.GetBlogsAsync(CancellationToken.None);

        Assert.Equal(2, blogs.Count);
    }

    [Fact]
    public async Task GetBlogsAsync_NoneFlaggedDefault_MarksFirstAsDefault()
    {
        var handler = new FixtureHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(FixtureFile.ReadText("config-two-blogs.json"), Encoding.UTF8, "application/json"),
        });
        var provider = BuildProvider(handler, "test-token");

        var blogs = await provider.GetBlogsAsync(CancellationToken.None);

        Assert.True(blogs[0].IsDefault);
        Assert.False(blogs[1].IsDefault);
    }

    [Fact]
    public async Task GetBlogsAsync_OneFlaggedDefault_KeepsTheFlaggedOne_RegardlessOfOrder()
    {
        var handler = new FixtureHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(FixtureFile.ReadText("config-two-blogs-with-default.json"), Encoding.UTF8, "application/json"),
        });
        var provider = BuildProvider(handler, "test-token");

        var blogs = await provider.GetBlogsAsync(CancellationToken.None);

        Assert.False(blogs[0].IsDefault);
        Assert.True(blogs[1].IsDefault);
    }

    [Fact]
    public async Task PublishAsync_NoStoredToken_Throws()
    {
        var handler = new FixtureHttpMessageHandler(_ => throw new InvalidOperationException("Should not call the network without a token."));
        var provider = BuildProvider(handler, token: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.PublishAsync(new PostDraft("x", null, false), CancellationToken.None));
    }

    [Fact]
    public async Task PublishAsync_Failure_DoesNotLeakTokenInExceptionMessage()
    {
        var handler = new FixtureHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(FixtureFile.ReadText("error-401.json"), Encoding.UTF8, "application/json"),
        });
        var provider = BuildProvider(handler, "super-secret-token");

        var ex = await Assert.ThrowsAsync<MicropubException>(() => provider.PublishAsync(new PostDraft("x", null, false), CancellationToken.None));

        Assert.DoesNotContain("super-secret-token", ex.Message);
        Assert.DoesNotContain("super-secret-token", ex.ToString());
    }

    [Fact]
    public void Id_IsMicroblog()
    {
        var provider = BuildProvider(new FixtureHttpMessageHandler(_ => throw new InvalidOperationException()), "test-token");

        Assert.Equal("microblog", provider.Id);
    }

    private static MicroBlogProvider BuildProvider(FixtureHttpMessageHandler handler, string? token)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") };
        var credentialStore = new InMemoryCredentialStore();
        if (token is not null)
        {
            credentialStore.Save(AccountId, token);
        }

        return new MicroBlogProvider(new MicropubClient(httpClient), credentialStore, AccountId);
    }
}