using System.Net;
using System.Net.Http.Headers;
using System.Text;

using Transom.Core.MicroBlog;
using Transom.Core.Models;
using Transom.Core.Tests.Fixtures;
using Transom.Core.Tests.Http;

namespace Transom.Core.Tests.MicroBlog;

public class MicropubClientPublishTests
{
    [Fact]
    public async Task PublishAsync_Published_ReturnsLocationAsUrl()
    {
        var handler = new FixtureHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Created);
            response.Headers.Location = new Uri("https://example.micro.blog/2026/09/21/hello.html");
            return response;
        });
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });

        var result = await client.PublishAsync("test-token", new PostDraft("Hello, world!", null, PostAsDraft: false), CancellationToken.None);

        Assert.Equal("https://example.micro.blog/2026/09/21/hello.html", result.Url);
        Assert.Null(result.PreviewUrl);
    }

    [Fact]
    public async Task PublishAsync_Draft_ReturnsUrlAndPreviewFromBody()
    {
        var handler = new FixtureHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Accepted)
        {
            Content = new StringContent(FixtureFile.ReadText("publish-draft-response.json"), Encoding.UTF8, "application/json"),
        });
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });

        var result = await client.PublishAsync("test-token", new PostDraft("Draft text", "A title", PostAsDraft: true), CancellationToken.None);

        Assert.Equal("https://example.micro.blog/2026/09/21/hello-world.html", result.Url);
        Assert.Equal("https://micro.blog/account/posts/123/preview/456", result.PreviewUrl);
    }

    [Fact]
    public async Task PublishAsync_SendsExpectedFormFields()
    {
        var handler = new FixtureHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Created);
            response.Headers.Location = new Uri("https://example.micro.blog/p.html");
            return response;
        });
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });

        await client.PublishAsync("test-token", new PostDraft("Body text", "My Title", PostAsDraft: true), CancellationToken.None);

        var form = await handler.LastRequest!.Content!.ReadAsStringAsync();
        Assert.Contains("h=entry", form);
        Assert.Contains("content=Body+text", form);
        Assert.Contains("name=My+Title", form);
        Assert.Contains("post-status=draft", form);
    }

    [Fact]
    public async Task PublishAsync_401_ThrowsWithoutLeakingToken()
    {
        var handler = new FixtureHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(FixtureFile.ReadText("error-401.json"), Encoding.UTF8, "application/json"),
        });
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });

        var ex = await Assert.ThrowsAsync<MicropubException>(() => client.PublishAsync("test-token", new PostDraft("x", null, false), CancellationToken.None));

        Assert.DoesNotContain("test-token", ex.Message);
    }
}