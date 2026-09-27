using System.Net;

using Transom.App.Services;
using Transom.Core.MicroBlog;

namespace Transom.App.Tests.Services;

public class ComposerErrorMessagesTests
{
    [Fact]
    public void Describe_NetworkFailure_ReturnsOfflineMessage()
    {
        var ex = new MicropubException(null, "No such host is known. (micro.blog:443)");

        Assert.Equal("You appear to be offline. Check your connection and try again.", ComposerErrorMessages.Describe(ex));
    }

    [Fact]
    public void Describe_Unauthorized_ReturnsTokenRejectedMessage()
    {
        var ex = new MicropubException(HttpStatusCode.Unauthorized, "App token was not valid.");

        Assert.Equal("Your app token was rejected. Paste a new one in Settings.", ComposerErrorMessages.Describe(ex));
    }

    [Fact]
    public void Describe_OtherServerError_ReturnsServerMessage()
    {
        var ex = new MicropubException(HttpStatusCode.RequestEntityTooLarge, "File is too large.");

        Assert.Equal("File is too large.", ComposerErrorMessages.Describe(ex));
    }

    [Fact]
    public void Describe_UnexpectedException_ReturnsItsMessage()
    {
        var ex = new InvalidOperationException("No Micro.blog account is signed in.");

        Assert.Equal("No Micro.blog account is signed in.", ComposerErrorMessages.Describe(ex));
    }
}