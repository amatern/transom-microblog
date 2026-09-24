using System.Net;
using System.Text;

using Transom.Core.MicroBlog;
using Transom.Core.Tests.Fixtures;
using Transom.Core.Tests.Http;

namespace Transom.Core.Tests.MicroBlog;

public class AccountClientTests
{
    [Fact]
    public async Task VerifyAsync_Success_ReturnsAccountInfo()
    {
        var handler = new FixtureHttpMessageHandler(request => request.RequestUri!.AbsolutePath == "/account/verify"
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(FixtureFile.ReadText("verify-success.json"), Encoding.UTF8, "application/json"),
            }
            : throw new InvalidOperationException("Unexpected request: " + request.RequestUri));
        var client = BuildClient(handler);

        var account = await client.VerifyAsync("pasted-token", CancellationToken.None);

        Assert.Equal("HIJKLMNOP", account.Token);
        Assert.Equal("testuser", account.Username);
    }

    [Fact]
    public async Task VerifyAsync_InvalidToken_ThrowsWithoutFallingBack()
    {
        var configRequested = false;
        var handler = new FixtureHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/account/verify")
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent(FixtureFile.ReadText("verify-error.json"), Encoding.UTF8, "application/json"),
                };
            }

            configRequested = true;
            throw new InvalidOperationException("q=config should not be called for a 401.");
        });
        var client = BuildClient(handler);

        var ex = await Assert.ThrowsAsync<MicropubException>(() => client.VerifyAsync("bad-token", CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.False(configRequested);
    }

    [Fact]
    public async Task VerifyAsync_ServerError_FallsBackToConfig()
    {
        var handler = new FixtureHttpMessageHandler(request => request.RequestUri!.AbsolutePath == "/account/verify"
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(FixtureFile.ReadText("error-500.json"), Encoding.UTF8, "application/json"),
            }
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(FixtureFile.ReadText("config-one-blog.json"), Encoding.UTF8, "application/json"),
            });
        var client = BuildClient(handler);

        var account = await client.VerifyAsync("test-token", CancellationToken.None);

        Assert.Equal("test-token", account.Token);
        Assert.Equal("https://example.micro.blog/", account.DefaultSite);
        Assert.Equal(string.Empty, account.Username);
    }

    private static AccountClient BuildClient(FixtureHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") };
        return new AccountClient(httpClient, new MicropubClient(httpClient));
    }
}