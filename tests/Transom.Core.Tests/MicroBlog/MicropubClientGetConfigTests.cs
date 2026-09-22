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
}