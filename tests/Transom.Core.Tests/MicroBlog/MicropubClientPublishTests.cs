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
    public async Task PublishAsync_Draft_KeepsPreviewFromBody_EvenWhenLocationHeaderIsAlsoPresent()
    {
        // SPEC.md §6.2 documents Location as the general success signal and `preview` as a body
        // field a draft response carries; it never says a server can't send both. If it does, the
        // body's `preview` must still win, or drafts would link callers to the 404-until-published
        // public URL — the exact bug this client exists to prevent.
        var handler = new FixtureHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent(FixtureFile.ReadText("publish-draft-response.json"), Encoding.UTF8, "application/json"),
            };
            response.Headers.Location = new Uri("https://example.micro.blog/2026/09/21/hello-world.html");
            return response;
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

    [Fact]
    public async Task PublishAsync_NoRouteToHost_ThrowsMicropubExceptionWithNullStatusCode()
    {
        // Publishing with no network connection must not let a raw HttpRequestException escape
        // uncaught — it becomes one domain exception type here, at the client layer, so every
        // caller (ComposerViewModel included) only ever handles one type.
        var handler = new ThrowingHttpMessageHandler(() => new HttpRequestException("No such host is known. (micro.blog:443)"));
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });

        var ex = await Assert.ThrowsAsync<MicropubException>(() => client.PublishAsync("test-token", new PostDraft("x", null, false), CancellationToken.None));

        Assert.Null(ex.StatusCode);
        Assert.DoesNotContain("test-token", ex.Message);
    }

    [Fact]
    public async Task PublishAsync_Timeout_ThrowsMicropubExceptionWithNullStatusCode()
    {
        var handler = new ThrowingHttpMessageHandler(() => new TaskCanceledException("The request timed out.", new TimeoutException()));
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });

        var ex = await Assert.ThrowsAsync<MicropubException>(() => client.PublishAsync("test-token", new PostDraft("x", null, false), CancellationToken.None));

        Assert.Null(ex.StatusCode);
    }

    [Fact]
    public async Task PublishAsync_WithTwoPhotos_SendsPhotoAndAltInMatchingOrder()
    {
        var handler = new FixtureHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Created);
            response.Headers.Location = new Uri("https://example.micro.blog/p.html");
            return response;
        });
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });
        var images = new[]
        {
            new DraftImage("https://cdn.micro.blog/uploads/one.jpg", "A red bicycle"),
            new DraftImage("https://cdn.micro.blog/uploads/two.jpg", null),
        };

        await client.PublishAsync("test-token", new PostDraft("Two photos", null, PostAsDraft: false, images), CancellationToken.None);

        var form = await handler.LastRequest!.Content!.ReadAsStringAsync();
        Assert.Equal(
            "h=entry&content=Two+photos&photo=https%3A%2F%2Fcdn.micro.blog%2Fuploads%2Fone.jpg&mp-photo-alt=A+red+bicycle&photo=https%3A%2F%2Fcdn.micro.blog%2Fuploads%2Ftwo.jpg&mp-photo-alt=",
            form);
    }

    [Fact]
    public async Task PublishAsync_NoImages_FormIsUnchangedFromBeforeM2()
    {
        // Review Focus #3: adding Images must not add stray empty photo[]/mp-photo-alt[] fields
        // to a text-only post.
        var handler = new FixtureHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Created);
            response.Headers.Location = new Uri("https://example.micro.blog/p.html");
            return response;
        });
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });

        await client.PublishAsync("test-token", new PostDraft("Just some text", null, PostAsDraft: false), CancellationToken.None);

        var form = await handler.LastRequest!.Content!.ReadAsStringAsync();
        Assert.DoesNotContain("photo", form);
    }
}