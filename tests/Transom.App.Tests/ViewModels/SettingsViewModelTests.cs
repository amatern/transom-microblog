using System.Net;

using Transom.App.ViewModels;
using Transom.Core.Credentials;
using Transom.Core.MicroBlog;

namespace Transom.App.Tests.ViewModels;

public class SettingsViewModelTests
{
    [Fact]
    public void VerifyCommand_Disabled_WhenTokenInputEmpty()
    {
        var vm = new SettingsViewModel(BuildAccountClient(_ => throw new InvalidOperationException()), new InMemoryCredentialStore());

        vm.TokenInput = string.Empty;

        Assert.False(vm.VerifyCommand.CanExecute(null));
    }

    [Fact]
    public async Task VerifyCommand_OnSuccess_PopulatesProfileAndStoresToken()
    {
        var credentialStore = new InMemoryCredentialStore();
        var vm = new SettingsViewModel(
            BuildAccountClient(token => Task.FromResult(new Transom.Core.Models.AccountInfo(token + "-verified", "Test User", "testuser", "https://micro.blog/testuser/avatar.jpg", "testuser.micro.blog", null))),
            credentialStore)
        {
            TokenInput = "pasted-token",
        };

        await vm.VerifyCommand.ExecuteAsync(null);

        Assert.Equal("Test User", vm.AccountName);
        Assert.Equal("testuser", vm.AccountUsername);
        Assert.Equal("https://micro.blog/testuser/avatar.jpg", vm.AvatarUrl);
        Assert.Null(vm.ErrorMessage);
        Assert.Equal("pasted-token-verified", credentialStore.TryGet(CredentialAccounts.Default));
    }

    [Fact]
    public async Task VerifyCommand_OnFailure_ShowsErrorAndDoesNotStoreToken()
    {
        var credentialStore = new InMemoryCredentialStore();
        var vm = new SettingsViewModel(
            BuildAccountClient(_ => throw new MicropubException(HttpStatusCode.Unauthorized, "App token was not valid.")),
            credentialStore)
        {
            TokenInput = "bad-token",
        };

        await vm.VerifyCommand.ExecuteAsync(null);

        Assert.Equal("App token was not valid.", vm.ErrorMessage);
        Assert.Null(credentialStore.TryGet(CredentialAccounts.Default));
        Assert.Null(vm.AccountName);
    }

    [Fact]
    public async Task VerifyCommand_Disabled_WhileVerifying()
    {
        var gate = new TaskCompletionSource();
        var vm = new SettingsViewModel(
            BuildAccountClient(async token => { await gate.Task; return new Transom.Core.Models.AccountInfo(token, "Test User", "testuser", null, null, null); }),
            new InMemoryCredentialStore())
        {
            TokenInput = "pasted-token",
        };

        var verifyTask = vm.VerifyCommand.ExecuteAsync(null);
        Assert.False(vm.VerifyCommand.CanExecute(null));

        gate.SetResult();
        await verifyTask;

        // The verify succeeded, which clears TokenInput (see
        // VerifyCommand_OnSuccess_PopulatesProfileAndStoresToken), so CanExecute would be false
        // again for that reason alone. Re-populate TokenInput to isolate what this test is
        // actually about: IsVerifying flipping back to false correctly un-forces CanExecute via
        // [NotifyCanExecuteChangedFor(nameof(VerifyCommand))].
        vm.TokenInput = "pasted-token";
        Assert.True(vm.VerifyCommand.CanExecute(null));
    }

    [Fact]
    public async Task LoadCommand_WithStoredToken_PopulatesProfile()
    {
        var credentialStore = new InMemoryCredentialStore();
        credentialStore.Save(CredentialAccounts.Default, "stored-token");
        var vm = new SettingsViewModel(
            BuildAccountClient(token => Task.FromResult(new Transom.Core.Models.AccountInfo(token + "-verified", "Test User", "testuser", "https://micro.blog/testuser/avatar.jpg", "testuser.micro.blog", null))),
            credentialStore);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Test User", vm.AccountName);
        Assert.Equal("testuser", vm.AccountUsername);
        Assert.Equal("https://micro.blog/testuser/avatar.jpg", vm.AvatarUrl);
        Assert.True(vm.IsSignedIn);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task LoadCommand_WithoutStoredToken_StaysSignedOut_AndNeverCallsVerify()
    {
        var verifyCalled = false;
        var vm = new SettingsViewModel(
            BuildAccountClient(_ =>
            {
                verifyCalled = true;
                throw new InvalidOperationException("Verify should not be called when there is no stored token.");
            }),
            new InMemoryCredentialStore());

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(verifyCalled);
        Assert.Null(vm.AccountUsername);
        Assert.False(vm.IsSignedIn);
    }

    [Fact]
    public async Task LoadCommand_WithInvalidStoredToken_ShowsFriendlyError_AndKeepsToken()
    {
        var credentialStore = new InMemoryCredentialStore();
        credentialStore.Save(CredentialAccounts.Default, "stale-token");
        var vm = new SettingsViewModel(
            BuildAccountClient(_ => throw new MicropubException(HttpStatusCode.Unauthorized, "App token was not valid.")),
            credentialStore);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Couldn't verify your saved token", vm.ErrorMessage);
        Assert.Equal("stale-token", credentialStore.TryGet(CredentialAccounts.Default));
        Assert.Null(vm.AccountUsername);
    }

    [Fact]
    public void SignOutCommand_ClearsCredentialStoreAndProfile()
    {
        var credentialStore = new InMemoryCredentialStore();
        credentialStore.Save(CredentialAccounts.Default, "stored-token");
        var vm = new SettingsViewModel(BuildAccountClient(_ => throw new InvalidOperationException()), credentialStore)
        {
            AccountName = "Test User",
            AccountUsername = "testuser",
            AvatarUrl = "https://micro.blog/testuser/avatar.jpg",
        };

        vm.SignOutCommand.Execute(null);

        Assert.Null(credentialStore.TryGet(CredentialAccounts.Default));
        Assert.Null(vm.AccountName);
        Assert.Null(vm.AccountUsername);
        Assert.Null(vm.AvatarUrl);
        Assert.False(vm.IsSignedIn);
    }

    private static AccountClient BuildAccountClient(Func<string, Task<Transom.Core.Models.AccountInfo>> verify)
    {
        var handler = new DelegatingVerifyHandler(verify);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") };
        return new AccountClient(httpClient, new MicropubClient(httpClient));
    }

    /// <summary>Routes /account/verify to the test's callback so SettingsViewModel tests don't
    /// need real HTTP fixtures — AccountClient itself is already covered by Task 3's tests.</summary>
    private sealed class DelegatingVerifyHandler : HttpMessageHandler
    {
        private readonly Func<string, Task<Transom.Core.Models.AccountInfo>> _verify;

        public DelegatingVerifyHandler(Func<string, Task<Transom.Core.Models.AccountInfo>> verify) => _verify = verify;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var form = await request.Content!.ReadAsStringAsync(cancellationToken);
            var token = System.Web.HttpUtility.ParseQueryString(form)["token"]!;

            try
            {
                var account = await _verify(token);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = System.Net.Http.Json.JsonContent.Create(account, Transom.Core.MicroBlog.TransomJsonContext.Default.AccountInfo),
                };
            }
            catch (MicropubException ex)
            {
                return new HttpResponseMessage(ex.StatusCode)
                {
                    Content = System.Net.Http.Json.JsonContent.Create(new Transom.Core.Models.ApiErrorResponse(ex.Message), Transom.Core.MicroBlog.TransomJsonContext.Default.ApiErrorResponse),
                };
            }
        }
    }
}