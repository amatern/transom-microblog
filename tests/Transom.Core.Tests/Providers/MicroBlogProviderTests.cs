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

    [Fact]
    public async Task UploadMediaAsync_FetchesConfigThenUploads_ReturnsUrl()
    {
        var calls = new List<string>();
        var handler = new FixtureHttpMessageHandler(request =>
        {
            calls.Add(request.RequestUri!.ToString());
            if (request.RequestUri!.Query.Contains("q=config"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(FixtureFile.ReadText("config-one-blog.json"), Encoding.UTF8, "application/json"),
                };
            }

            var response = new HttpResponseMessage(HttpStatusCode.Accepted);
            response.Headers.Location = new Uri("https://cdn.micro.blog/uploads/photo.jpg");
            return response;
        });
        var provider = BuildProvider(handler, "test-token");
        using var data = new MemoryStream(new byte[10]);

        var result = await provider.UploadMediaAsync(data, "photo.jpg", "image/jpeg", null, CancellationToken.None);

        Assert.Equal("https://cdn.micro.blog/uploads/photo.jpg", result.Url);
        Assert.Contains(calls, c => c.Contains("q=config"));
        Assert.Contains(calls, c => c.Contains("/micropub/media"));
    }

    [Fact]
    public async Task UploadMediaAsync_SecondCall_DoesNotRefetchConfig()
    {
        var configFetches = 0;
        var handler = new FixtureHttpMessageHandler(request =>
        {
            if (request.RequestUri!.Query.Contains("q=config"))
            {
                configFetches++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(FixtureFile.ReadText("config-one-blog.json"), Encoding.UTF8, "application/json"),
                };
            }

            var response = new HttpResponseMessage(HttpStatusCode.Accepted);
            response.Headers.Location = new Uri("https://cdn.micro.blog/uploads/photo.jpg");
            return response;
        });
        var provider = BuildProvider(handler, "test-token");

        await provider.UploadMediaAsync(new MemoryStream(new byte[10]), "a.jpg", "image/jpeg", null, CancellationToken.None);
        await provider.UploadMediaAsync(new MemoryStream(new byte[10]), "b.jpg", "image/jpeg", null, CancellationToken.None);

        Assert.Equal(1, configFetches);
    }

    [Fact]
    public async Task UploadMediaAsync_NoStoredToken_Throws()
    {
        var handler = new FixtureHttpMessageHandler(_ => throw new InvalidOperationException("Should not call the network without a token."));
        var provider = BuildProvider(handler, token: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.UploadMediaAsync(new MemoryStream(new byte[10]), "a.jpg", "image/jpeg", null, CancellationToken.None));
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