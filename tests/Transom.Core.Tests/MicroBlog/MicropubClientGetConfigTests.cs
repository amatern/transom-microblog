using System.Net;

using Transom.Core.MicroBlog;
using Transom.Core.Tests.Fixtures;
using Transom.Core.Tests.Http;

namespace Transom.Core.Tests.MicroBlog;

public class MicropubClientGetConfigTests
{
    [Fact]
    public async Task GetConfigAsync_OneBlog_ReturnsConfig()
    {
        var handler = new FixtureHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(FixtureFile.ReadText("config-one-blog.json"), System.Text.Encoding.UTF8, "application/json"),
        });
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });

        var config = await client.GetConfigAsync("test-token", CancellationToken.None);

        Assert.Single(config.Destinations);
        Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization!.Scheme);
        Assert.Equal("test-token", handler.LastRequest.Headers.Authorization!.Parameter);
    }

    [Fact]
    public async Task GetConfigAsync_RealAccountShape_MapsDestinationFieldsAndIgnoresUnknownOnes()
    {
        // SPEC.md §6.2: config-one-blog.json now matches a real account's q=config response —
        // destination[] is present for a single blog (the docs' minimal example just omits it
        // entirely), and the response also carries post-types/channels/syndicate-to, none of which
        // Transom models yet. Confirms none of that trips up deserialization.
        var handler = new FixtureHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(FixtureFile.ReadText("config-one-blog.json"), System.Text.Encoding.UTF8, "application/json"),
        });
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });

        var config = await client.GetConfigAsync("test-token", CancellationToken.None);

        var blog = Assert.Single(config.Destinations);
        Assert.Equal("https://example.micro.blog/", blog.Uid);
        Assert.Equal("example.micro.blog", blog.Name);
        Assert.Equal("Example Blog", blog.Title);
        Assert.Equal("Example Blog", blog.DisplayName);
        Assert.True(blog.IsDefault);
    }

    [Fact]
    public async Task GetConfigAsync_TwoBlogs_ReturnsConfig()
    {
        var handler = new FixtureHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(FixtureFile.ReadText("config-two-blogs.json"), System.Text.Encoding.UTF8, "application/json"),
        });
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });

        var config = await client.GetConfigAsync("test-token", CancellationToken.None);

        Assert.Equal(2, config.Destinations.Count);
    }

    [Fact]
    public async Task GetConfigAsync_401_ThrowsMicropubException()
    {
        var handler = new FixtureHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(FixtureFile.ReadText("error-401.json"), System.Text.Encoding.UTF8, "application/json"),
        });
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });

        var ex = await Assert.ThrowsAsync<MicropubException>(() => client.GetConfigAsync("test-token", CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.DoesNotContain("test-token", ex.Message);
    }

    [Fact]
    public async Task GetConfigAsync_500_ThrowsMicropubException()
    {
        var handler = new FixtureHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(FixtureFile.ReadText("error-500.json"), System.Text.Encoding.UTF8, "application/json"),
        });
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });

        var ex = await Assert.ThrowsAsync<MicropubException>(() => client.GetConfigAsync("test-token", CancellationToken.None));

        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
    }

    [Fact]
    public async Task GetConfigAsync_NoRouteToHost_ThrowsMicropubExceptionWithNullStatusCode()
    {
        var handler = new ThrowingHttpMessageHandler(() => new HttpRequestException("No such host is known. (micro.blog:443)"));
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });

        var ex = await Assert.ThrowsAsync<MicropubException>(() => client.GetConfigAsync("test-token", CancellationToken.None));

        Assert.Null(ex.StatusCode);
        Assert.DoesNotContain("test-token", ex.Message);
    }

    [Fact]
    public async Task GetConfigAsync_Timeout_ThrowsMicropubExceptionWithNullStatusCode()
    {
        var handler = new ThrowingHttpMessageHandler(() => new TaskCanceledException("The request timed out.", new TimeoutException()));
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });

        var ex = await Assert.ThrowsAsync<MicropubException>(() => client.GetConfigAsync("test-token", CancellationToken.None));

        Assert.Null(ex.StatusCode);
    }

    [Fact]
    public async Task GetConfigAsync_CallerCancels_ThrowsTaskCanceledException_NotMicropubException()
    {
        var handler = new ThrowingHttpMessageHandler(() => new TaskCanceledException());
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(() => client.GetConfigAsync("test-token", cts.Token));
    }
}