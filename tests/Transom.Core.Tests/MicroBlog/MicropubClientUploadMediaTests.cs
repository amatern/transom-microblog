using System.Net;
using System.Text;

using Transom.Core.MicroBlog;
using Transom.Core.Tests.Fixtures;
using Transom.Core.Tests.Http;

namespace Transom.Core.Tests.MicroBlog;

public class MicropubClientUploadMediaTests
{
    private const string MediaEndpoint = "https://micro.blog/micropub/media";

    [Fact]
    public async Task UploadMediaAsync_Accepted_ReturnsLocationAsUrl()
    {
        var handler = new FixtureHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Accepted);
            response.Headers.Location = new Uri("https://cdn.micro.blog/uploads/photo.jpg");
            return response;
        });
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });
        using var data = new MemoryStream(new byte[1024]);

        var result = await client.UploadMediaAsync("test-token", MediaEndpoint, data, "photo.jpg", "image/jpeg", progress: null, CancellationToken.None);

        Assert.Equal("https://cdn.micro.blog/uploads/photo.jpg", result.Url);
    }

    [Fact]
    public async Task UploadMediaAsync_SendsMultipartFilePart()
    {
        var handler = new FixtureHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Accepted);
            response.Headers.Location = new Uri("https://cdn.micro.blog/uploads/photo.jpg");
            return response;
        });
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });
        using var data = new MemoryStream(Encoding.UTF8.GetBytes("fake-image-bytes"));

        await client.UploadMediaAsync("test-token", MediaEndpoint, data, "photo.jpg", "image/jpeg", progress: null, CancellationToken.None);

        Assert.Equal(MediaEndpoint, handler.LastRequest!.RequestUri!.ToString());
        var body = await handler.LastRequest.Content!.ReadAsStringAsync();
        Assert.Contains("filename=\"photo.jpg\"", body);
        Assert.Contains("fake-image-bytes", body);
    }

    [Fact]
    public async Task UploadMediaAsync_ReportsProgress()
    {
        var handler = new FixtureHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Accepted);
            response.Headers.Location = new Uri("https://cdn.micro.blog/uploads/photo.jpg");
            return response;
        });
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });
        using var data = new MemoryStream(new byte[50_000]);
        var reported = new List<double>();

        await client.UploadMediaAsync("test-token", MediaEndpoint, data, "photo.jpg", "image/jpeg", new Progress<double>(reported.Add), CancellationToken.None);

        Assert.Contains(1.0, reported);
    }

    [Fact]
    public async Task UploadMediaAsync_NoLocationHeader_ThrowsMicropubException()
    {
        var handler = new FixtureHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Accepted));
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });
        using var data = new MemoryStream(new byte[10]);

        var ex = await Assert.ThrowsAsync<MicropubException>(() =>
            client.UploadMediaAsync("test-token", MediaEndpoint, data, "photo.jpg", "image/jpeg", null, CancellationToken.None));

        Assert.Contains("no media URL", ex.Message);
    }

    [Fact]
    public async Task UploadMediaAsync_TooLarge_ThrowsWithoutLeakingToken()
    {
        var handler = new FixtureHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.RequestEntityTooLarge)
        {
            Content = new StringContent(FixtureFile.ReadText("error-media-rejected.json"), Encoding.UTF8, "application/json"),
        });
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });
        using var data = new MemoryStream(new byte[10]);

        var ex = await Assert.ThrowsAsync<MicropubException>(() =>
            client.UploadMediaAsync("super-secret-token", MediaEndpoint, data, "photo.jpg", "image/jpeg", null, CancellationToken.None));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, ex.StatusCode);
        Assert.Equal("File is too large.", ex.Message);
        Assert.DoesNotContain("super-secret-token", ex.ToString());
    }

    [Fact]
    public async Task UploadMediaAsync_WrongType_ThrowsMicropubException()
    {
        var handler = new FixtureHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.UnsupportedMediaType)
        {
            Content = new StringContent(FixtureFile.ReadText("error-media-rejected.json"), Encoding.UTF8, "application/json"),
        });
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });
        using var data = new MemoryStream(new byte[10]);

        var ex = await Assert.ThrowsAsync<MicropubException>(() =>
            client.UploadMediaAsync("test-token", MediaEndpoint, data, "photo.bmp", "image/bmp", null, CancellationToken.None));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, ex.StatusCode);
    }
}
