using Transom.App.Services;
using Transom.Core.Models;

namespace Transom.App.Tests.Services;

public class AccountStateServiceTests
{
    [Fact]
    public void InitialState_IsSignedOut()
    {
        var account = new AccountStateService();

        Assert.False(account.IsSignedIn);
        Assert.Equal("Signed out", account.DisplayName);
    }

    [Fact]
    public void SetProfile_WithName_ShowsNameAsDisplayName()
    {
        var account = new AccountStateService();

        account.SetProfile(new AccountInfo("token", "Test User", "testuser", "https://micro.blog/testuser/avatar.jpg", null, null));

        Assert.True(account.IsSignedIn);
        Assert.Equal("Test User", account.DisplayName);
        Assert.Equal("https://micro.blog/testuser/avatar.jpg", account.AvatarUrl);
    }

    [Fact]
    public void SetProfile_WithoutName_FallsBackToAtUsername()
    {
        var account = new AccountStateService();

        account.SetProfile(new AccountInfo("token", Name: null, Username: "testuser", Avatar: null, DefaultSite: null, ExpiresAt: null));

        Assert.Equal("@testuser", account.DisplayName);
    }

    [Fact]
    public void SetProfile_WithNeitherNameNorUsername_StillReportsSignedIn()
    {
        // AccountClient's config-only fallback (SPEC.md §6.1) returns an empty username, not null,
        // when only a token — not a profile — could be confirmed.
        var account = new AccountStateService();

        account.SetProfile(new AccountInfo("token", Name: null, Username: string.Empty, Avatar: null, DefaultSite: null, ExpiresAt: null));

        Assert.True(account.IsSignedIn);
        Assert.Equal("Signed in", account.DisplayName);
    }

    [Fact]
    public void Clear_ReturnsToSignedOut()
    {
        var account = new AccountStateService();
        account.SetProfile(new AccountInfo("token", "Test User", "testuser", "https://micro.blog/testuser/avatar.jpg", null, null));

        account.Clear();

        Assert.False(account.IsSignedIn);
        Assert.Equal("Signed out", account.DisplayName);
        Assert.Null(account.AvatarUrl);
    }
}