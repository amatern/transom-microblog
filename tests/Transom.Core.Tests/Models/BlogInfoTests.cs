using Transom.Core.Models;

namespace Transom.Core.Tests.Models;

public class BlogInfoTests
{
    [Fact]
    public void DisplayName_PrefersTitle_OverName()
    {
        var blog = new BlogInfo("https://example.micro.blog/", "example.micro.blog", "Example Blog");

        Assert.Equal("Example Blog", blog.DisplayName);
    }

    [Fact]
    public void DisplayName_FallsBackToName_WhenTitleMissing()
    {
        var blog = new BlogInfo("https://example.micro.blog/", "example.micro.blog", Title: null);

        Assert.Equal("example.micro.blog", blog.DisplayName);
    }

    [Fact]
    public void IsDefault_DefaultsToFalse_WhenNotSpecified()
    {
        var blog = new BlogInfo("https://example.micro.blog/", "example.micro.blog", "Example Blog");

        Assert.False(blog.IsDefault);
    }
}