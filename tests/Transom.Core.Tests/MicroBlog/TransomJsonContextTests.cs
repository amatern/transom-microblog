using System.Text.Json;

using Transom.Core.MicroBlog;
using Transom.Core.Models;
using Transom.Core.Tests.Fixtures;

namespace Transom.Core.Tests.MicroBlog;

public class TransomJsonContextTests
{
    [Fact]
    public void MicropubConfig_DeserializesOneBlog()
    {
        using var stream = FixtureFile.OpenRead("config-one-blog.json");

        var config = JsonSerializer.Deserialize(stream, TransomJsonContext.Default.MicropubConfig);

        Assert.NotNull(config);
        Assert.Equal("https://micro.blog/micropub/media", config!.MediaEndpoint);
        var blog = Assert.Single(config.Destinations);
        Assert.Equal("https://example.micro.blog/", blog.Uid);
        Assert.Equal("example.micro.blog", blog.Name);
        Assert.Equal("Example Blog", blog.Title);
    }

    [Fact]
    public void MicropubConfig_DeserializesTwoBlogs()
    {
        using var stream = FixtureFile.OpenRead("config-two-blogs.json");

        var config = JsonSerializer.Deserialize(stream, TransomJsonContext.Default.MicropubConfig);

        Assert.NotNull(config);
        Assert.Equal(2, config!.Destinations.Count);
        Assert.Equal("https://myblog.micro.blog/", config.Destinations[0].Uid);
        Assert.Equal("https://anotherblog.micro.blog/", config.Destinations[1].Uid);
    }

    [Fact]
    public void AccountInfo_DeserializesVerifySuccess()
    {
        using var stream = FixtureFile.OpenRead("verify-success.json");

        var account = JsonSerializer.Deserialize(stream, TransomJsonContext.Default.AccountInfo);

        Assert.NotNull(account);
        Assert.Equal("HIJKLMNOP", account!.Token);
        Assert.Equal("testuser", account.Username);
        Assert.Equal("testuser.micro.blog", account.DefaultSite);
    }

    [Fact]
    public void ApiErrorResponse_DeserializesVerifyError()
    {
        using var stream = FixtureFile.OpenRead("verify-error.json");

        var error = JsonSerializer.Deserialize(stream, TransomJsonContext.Default.ApiErrorResponse);

        Assert.NotNull(error);
        Assert.Equal("App token was not valid.", error!.Error);
    }
}