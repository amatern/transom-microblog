using Transom.App.Tests.TestDoubles;
using Transom.App.ViewModels;
using Transom.Core.Credentials;
using Transom.Core.MicroBlog;
using Transom.Core.Models;

namespace Transom.App.Tests.ViewModels;

public class ComposerViewModelTests
{
    [Fact]
    public void CharacterCount_ReflectsText()
    {
        var vm = BuildViewModel();

        vm.Text = "Hello";

        Assert.Equal(5, vm.CharacterCount);
    }

    [Fact]
    public void ShowTitleField_HiddenAt300Characters_VisibleAt301()
    {
        var vm = BuildViewModel();

        vm.Text = new string('x', 300);
        Assert.False(vm.ShowTitleField);

        vm.Text = new string('x', 301);
        Assert.True(vm.ShowTitleField);
    }

    [Fact]
    public void PublishCommand_Disabled_WhenTextEmpty()
    {
        var vm = BuildViewModel();

        vm.Text = string.Empty;

        Assert.False(vm.PublishCommand.CanExecute(null));
    }

    [Fact]
    public async Task PublishCommand_Disabled_WhileInFlight()
    {
        var provider = new FakeBlogProvider();
        var gate = new TaskCompletionSource();
        provider.OnPublish = async (_, _) =>
        {
            await gate.Task;
            return new PublishResult("https://example.micro.blog/post.html", null);
        };
        var vm = new ComposerViewModel(provider, new FakeComposerSettings(), SignedInCredentialStore()) { Text = "Hello" };

        var publishTask = vm.PublishCommand.ExecuteAsync(null);
        Assert.False(vm.PublishCommand.CanExecute(null));

        gate.SetResult();
        await publishTask;

        // The publish succeeded, which clears Text (see PublishCommand_OnSuccess_...), so
        // CanExecute would be false again for that reason alone. Re-populate Text to isolate
        // what this test is actually about: IsPublishing flipping back to false correctly
        // un-forces CanExecute via [NotifyCanExecuteChangedFor(nameof(PublishCommand))].
        vm.Text = "Hello again";
        Assert.True(vm.PublishCommand.CanExecute(null));
    }

    [Fact]
    public async Task PublishCommand_OnFailure_KeepsText_AndSetsErrorMessage()
    {
        var provider = new FakeBlogProvider
        {
            OnPublish = (_, _) => throw new MicropubException(System.Net.HttpStatusCode.Unauthorized, "App token was not valid."),
        };
        var vm = new ComposerViewModel(provider, new FakeComposerSettings(), SignedInCredentialStore()) { Text = "Don't lose me" };

        await vm.PublishCommand.ExecuteAsync(null);

        Assert.Equal("Don't lose me", vm.Text);
        Assert.Equal("App token was not valid.", vm.ErrorMessage);
        Assert.Null(vm.PublishedUrl);
    }

    [Fact]
    public async Task PublishCommand_OnSuccess_ClearsTextAndSetsPublishedUrl()
    {
        var provider = new FakeBlogProvider();
        var vm = new ComposerViewModel(provider, new FakeComposerSettings(), SignedInCredentialStore()) { Text = "Hello, world!" };

        await vm.PublishCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, vm.Text);
        Assert.Equal("https://example.micro.blog/post.html", vm.PublishedUrl);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task PublishCommand_PassesDraftFlagFromSettings()
    {
        var provider = new FakeBlogProvider();
        var settings = new FakeComposerSettings { PostAsDraft = true };
        var vm = new ComposerViewModel(provider, settings, SignedInCredentialStore()) { Text = "Hello" };

        await vm.PublishCommand.ExecuteAsync(null);

        Assert.True(provider.LastDraft!.PostAsDraft);
    }

    [Fact]
    public async Task PublishCommand_OnSuccess_Published_LinksToPublicUrl()
    {
        var provider = new FakeBlogProvider
        {
            OnPublish = (_, _) => Task.FromResult(new PublishResult("https://example.micro.blog/post.html", null)),
        };
        var vm = new ComposerViewModel(provider, new FakeComposerSettings { PostAsDraft = false }, SignedInCredentialStore()) { Text = "Hello" };

        await vm.PublishCommand.ExecuteAsync(null);

        Assert.False(vm.PublishedAsDraft);
        Assert.Equal("Published", vm.PublishSuccessTitle);
        Assert.Equal("https://example.micro.blog/post.html", vm.PublishedUrl);
        Assert.Equal(new Uri("https://example.micro.blog/post.html"), vm.PublishedUri);
    }

    [Fact]
    public async Task PublishCommand_OnSuccess_Draft_LinksToPreviewUrl_NotThePublicUrl()
    {
        // SPEC.md §6.2: a draft response's `url` is the eventual public URL, which 404s until the
        // post is actually published. The bar must link to `preview`, not `url`.
        var provider = new FakeBlogProvider
        {
            OnPublish = (_, _) => Task.FromResult(new PublishResult(
                "https://example.micro.blog/2026/09/22/transom-test.html",
                "https://micro.blog/account/posts/123/preview/456")),
        };
        var settings = new FakeComposerSettings { PostAsDraft = true };
        var vm = new ComposerViewModel(provider, settings, SignedInCredentialStore()) { Text = "Hello" };

        await vm.PublishCommand.ExecuteAsync(null);

        Assert.True(vm.PublishedAsDraft);
        Assert.Equal("Saved as draft", vm.PublishSuccessTitle);
        Assert.Equal("https://micro.blog/account/posts/123/preview/456", vm.PublishedUrl);
    }

    [Fact]
    public async Task PublishCommand_ResetsPreviousSuccess_WhenARetryFails()
    {
        var provider = new FakeBlogProvider
        {
            OnPublish = (_, _) => throw new MicropubException(System.Net.HttpStatusCode.Unauthorized, "App token was not valid."),
        };
        var vm = new ComposerViewModel(provider, new FakeComposerSettings(), SignedInCredentialStore()) { Text = "Hello", PublishedUrl = "https://example.micro.blog/old-post.html" };

        await vm.PublishCommand.ExecuteAsync(null);

        Assert.Null(vm.PublishedUrl);
        Assert.Equal("App token was not valid.", vm.ErrorMessage);
    }

    [Fact]
    public void PublishedUri_Null_WhenPublishedUrlIsMissingOrMalformed()
    {
        var vm = BuildViewModel();

        Assert.Null(vm.PublishedUri);
        Assert.False(vm.HasPublishedUri);
    }

    [Fact]
    public void ShowSignInHint_True_WhenNoTokenStored()
    {
        var vm = new ComposerViewModel(new FakeBlogProvider(), new FakeComposerSettings(), new InMemoryCredentialStore());

        Assert.True(vm.ShowSignInHint);
        Assert.False(vm.IsSignedIn);
    }

    [Fact]
    public void ShowSignInHint_False_WhenTokenStored()
    {
        var vm = new ComposerViewModel(new FakeBlogProvider(), new FakeComposerSettings(), SignedInCredentialStore());

        Assert.False(vm.ShowSignInHint);
        Assert.True(vm.IsSignedIn);
    }

    [Fact]
    public void PublishCommand_Disabled_WhenNotSignedIn_EvenWithText()
    {
        var vm = new ComposerViewModel(new FakeBlogProvider(), new FakeComposerSettings(), new InMemoryCredentialStore())
        {
            Text = "Hello",
        };

        Assert.False(vm.PublishCommand.CanExecute(null));
    }

    private static ComposerViewModel BuildViewModel() => new(new FakeBlogProvider(), new FakeComposerSettings(), SignedInCredentialStore());

    private static InMemoryCredentialStore SignedInCredentialStore()
    {
        var store = new InMemoryCredentialStore();
        store.Save(CredentialAccounts.Default, "test-token");
        return store;
    }
}