using Transom.App.Tests.TestDoubles;
using Transom.App.ViewModels;
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
        var vm = new ComposerViewModel(provider, new FakeComposerSettings()) { Text = "Hello" };

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
        var vm = new ComposerViewModel(provider, new FakeComposerSettings()) { Text = "Don't lose me" };

        await vm.PublishCommand.ExecuteAsync(null);

        Assert.Equal("Don't lose me", vm.Text);
        Assert.Equal("App token was not valid.", vm.ErrorMessage);
        Assert.Null(vm.PublishedUrl);
    }

    [Fact]
    public async Task PublishCommand_OnSuccess_ClearsTextAndSetsPublishedUrl()
    {
        var provider = new FakeBlogProvider();
        var vm = new ComposerViewModel(provider, new FakeComposerSettings()) { Text = "Hello, world!" };

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
        var vm = new ComposerViewModel(provider, settings) { Text = "Hello" };

        await vm.PublishCommand.ExecuteAsync(null);

        Assert.True(provider.LastDraft!.PostAsDraft);
    }

    private static ComposerViewModel BuildViewModel() => new(new FakeBlogProvider(), new FakeComposerSettings());
}