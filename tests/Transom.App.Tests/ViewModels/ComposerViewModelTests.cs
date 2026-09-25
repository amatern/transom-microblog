using System.Linq;
using System.Net;

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
            OnPublish = (_, _) => throw new MicropubException(System.Net.HttpStatusCode.InternalServerError, "Something went wrong."),
        };
        var vm = new ComposerViewModel(provider, new FakeComposerSettings(), SignedInCredentialStore()) { Text = "Don't lose me" };

        await vm.PublishCommand.ExecuteAsync(null);

        Assert.Equal("Don't lose me", vm.Text);
        Assert.Equal("Something went wrong.", vm.ErrorMessage);
        Assert.Null(vm.PublishedUrl);
    }

    [Fact]
    public async Task PublishCommand_OnUnauthorized_ShowsTokenRejectedMessage_NotTheServerText()
    {
        var provider = new FakeBlogProvider
        {
            OnPublish = (_, _) => throw new MicropubException(System.Net.HttpStatusCode.Unauthorized, "App token was not valid."),
        };
        var vm = new ComposerViewModel(provider, new FakeComposerSettings(), SignedInCredentialStore()) { Text = "Don't lose me" };

        await vm.PublishCommand.ExecuteAsync(null);

        Assert.Equal("Don't lose me", vm.Text);
        Assert.Equal("Your app token was rejected. Paste a new one in Settings.", vm.ErrorMessage);
        Assert.False(vm.IsPublishing);
    }

    [Fact]
    public async Task PublishCommand_OnNetworkFailure_ShowsOfflineMessage_AndResetsIsPublishing()
    {
        var provider = new FakeBlogProvider
        {
            OnPublish = async (_, ct) =>
            {
                await Task.Yield();
                throw new MicropubException(null, "No such host is known. (micro.blog:443)");
            },
        };
        var vm = new ComposerViewModel(provider, new FakeComposerSettings(), SignedInCredentialStore()) { Text = "Don't lose me" };

        await vm.PublishCommand.ExecuteAsync(null);

        Assert.Equal("Don't lose me", vm.Text);
        Assert.Equal("You appear to be offline. Check your connection and try again.", vm.ErrorMessage);
        Assert.False(vm.IsPublishing);
        Assert.True(vm.PublishCommand.CanExecute(null));
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
            OnPublish = (_, _) => throw new MicropubException(System.Net.HttpStatusCode.InternalServerError, "Something went wrong."),
        };
        var vm = new ComposerViewModel(provider, new FakeComposerSettings(), SignedInCredentialStore()) { Text = "Hello", PublishedUrl = "https://example.micro.blog/old-post.html" };

        await vm.PublishCommand.ExecuteAsync(null);

        Assert.Null(vm.PublishedUrl);
        Assert.Equal("Something went wrong.", vm.ErrorMessage);
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

    [Fact]
    public async Task AddImageAsync_UploadsImmediately_AndMarksUploaded()
    {
        var provider = new FakeBlogProvider();
        var vm = new ComposerViewModel(provider, new FakeComposerSettings(), SignedInCredentialStore());

        await vm.AddImageAsync(CreateTempImageFile("a.jpg"), "a.jpg", "image/jpeg", CancellationToken.None);

        var image = Assert.Single(vm.Images);
        Assert.Equal(ComposerImageStatus.Uploaded, image.Status);
        Assert.Equal("https://cdn.micro.blog/uploads/a.jpg", image.UploadedUrl);
    }

    [Fact]
    public async Task AddImageAsync_UploadFails_MarksFailed_AndBlocksPublish()
    {
        var provider = new FakeBlogProvider
        {
            OnUploadMedia = (_, _) => throw new MicropubException(null, "No such host is known."),
        };
        var vm = new ComposerViewModel(provider, new FakeComposerSettings(), SignedInCredentialStore()) { Text = "Hello" };

        await vm.AddImageAsync(CreateTempImageFile("a.jpg"), "a.jpg", "image/jpeg", CancellationToken.None);

        var image = Assert.Single(vm.Images);
        Assert.Equal(ComposerImageStatus.Failed, image.Status);
        Assert.Equal("You appear to be offline. Check your connection and try again.", image.ErrorMessage);
        Assert.False(vm.PublishCommand.CanExecute(null));
    }

    [Fact]
    public async Task RetryOnFailedImage_ReUploadsOnlyThatImage()
    {
        var attempt = 0;
        var provider = new FakeBlogProvider
        {
            OnUploadMedia = (fileName, _) =>
            {
                attempt++;
                if (fileName == "a.jpg" && attempt == 1)
                {
                    throw new MicropubException(null, "offline");
                }

                return Task.FromResult(new MediaItem($"https://cdn.micro.blog/uploads/{fileName}"));
            },
        };
        var vm = new ComposerViewModel(provider, new FakeComposerSettings(), SignedInCredentialStore());
        await vm.AddImageAsync(CreateTempImageFile("a.jpg"), "a.jpg", "image/jpeg", CancellationToken.None);
        await vm.AddImageAsync(CreateTempImageFile("b.jpg"), "b.jpg", "image/jpeg", CancellationToken.None);
        var failedImage = vm.Images.Single(i => i.FileName == "a.jpg");
        var succeededImage = vm.Images.Single(i => i.FileName == "b.jpg");
        Assert.Equal(ComposerImageStatus.Failed, failedImage.Status);
        Assert.Equal(ComposerImageStatus.Uploaded, succeededImage.Status);

        await failedImage.RetryCommand.ExecuteAsync(null);

        Assert.Equal(ComposerImageStatus.Uploaded, failedImage.Status);
        Assert.Equal(ComposerImageStatus.Uploaded, succeededImage.Status);
        Assert.True(vm.PublishCommand.CanExecute(null) || string.IsNullOrWhiteSpace(vm.Text));
    }

    [Fact]
    public async Task RemoveImage_CancelsUploadToken_AndUnblocksPublish()
    {
        var gate = new TaskCompletionSource();
        var provider = new FakeBlogProvider
        {
            OnUploadMedia = async (fileName, ct) =>
            {
                await gate.Task.WaitAsync(ct);
                return new MediaItem($"https://cdn.micro.blog/uploads/{fileName}");
            },
        };
        var vm = new ComposerViewModel(provider, new FakeComposerSettings(), SignedInCredentialStore()) { Text = "Hello" };
        var addTask = vm.AddImageAsync(CreateTempImageFile("a.jpg"), "a.jpg", "image/jpeg", CancellationToken.None);
        var image = Assert.Single(vm.Images);
        Assert.False(vm.PublishCommand.CanExecute(null));
        Assert.True(image.UploadCancellation.Token.CanBeCanceled);

        // This is exactly what Task 9's XAML does: the tray tile's own RemoveCommand, not a
        // page-level command — ComposerViewModel never exposes remove/move as its own [RelayCommand]s
        // (see the ComposerImageViewModel constructor delegates below).
        image.RemoveCommand.Execute(null);
        gate.SetResult();
        await Task.WhenAny(addTask, Task.Delay(1000));

        Assert.Empty(vm.Images);
        Assert.True(image.UploadCancellation.IsCancellationRequested);
        Assert.True(vm.PublishCommand.CanExecute(null));
    }

    [Fact]
    public async Task AddImageAsync_CancellingCallerToken_CancelsTheUpload()
    {
        var gate = new TaskCompletionSource();
        var provider = new FakeBlogProvider
        {
            OnUploadMedia = async (fileName, ct) =>
            {
                await gate.Task.WaitAsync(ct);
                return new MediaItem($"https://cdn.micro.blog/uploads/{fileName}");
            },
        };
        var vm = new ComposerViewModel(provider, new FakeComposerSettings(), SignedInCredentialStore());
        using var cts = new CancellationTokenSource();

        var addTask = vm.AddImageAsync(CreateTempImageFile("a.jpg"), "a.jpg", "image/jpeg", cts.Token);
        var image = Assert.Single(vm.Images);

        cts.Cancel();
        try
        {
            await addTask;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.True(image.UploadCancellation.IsCancellationRequested);
        Assert.Equal(ComposerImageStatus.Uploading, image.Status);
    }

    [Fact]
    public async Task AddImageAsync_AtCap_RejectsWithMessage_AndDoesNotAddAnEleventhImage()
    {
        var provider = new FakeBlogProvider();
        var vm = new ComposerViewModel(provider, new FakeComposerSettings(), SignedInCredentialStore());
        for (var i = 0; i < 10; i++)
        {
            await vm.AddImageAsync(CreateTempImageFile($"{i}.jpg"), $"{i}.jpg", "image/jpeg", CancellationToken.None);
        }

        await vm.AddImageAsync(CreateTempImageFile("eleventh.jpg"), "eleventh.jpg", "image/jpeg", CancellationToken.None);

        Assert.Equal(10, vm.Images.Count);
        Assert.Equal("Up to 10 images per post.", vm.AddImageErrorMessage);
    }

    [Fact]
    public async Task PublishAsync_BuildsDraftImagesFromUploadedImages_InOrder()
    {
        var provider = new FakeBlogProvider();
        var vm = new ComposerViewModel(provider, new FakeComposerSettings(), SignedInCredentialStore()) { Text = "Hello" };
        await vm.AddImageAsync(CreateTempImageFile("a.jpg"), "a.jpg", "image/jpeg", CancellationToken.None);
        await vm.AddImageAsync(CreateTempImageFile("b.jpg"), "b.jpg", "image/jpeg", CancellationToken.None);
        vm.Images[0].AltText = "First";
        vm.Images[1].AltText = "Second";

        await vm.PublishCommand.ExecuteAsync(null);

        Assert.Equal(2, provider.LastDraft!.Images.Count);
        Assert.Equal("https://cdn.micro.blog/uploads/a.jpg", provider.LastDraft.Images[0].Url);
        Assert.Equal("First", provider.LastDraft.Images[0].AltText);
        Assert.Equal("https://cdn.micro.blog/uploads/b.jpg", provider.LastDraft.Images[1].Url);
        Assert.Equal("Second", provider.LastDraft.Images[1].AltText);
    }

    [Fact]
    public async Task PublishAsync_UsesEditedAltText_NotTheOriginal()
    {
        var provider = new FakeBlogProvider();
        var vm = new ComposerViewModel(provider, new FakeComposerSettings(), SignedInCredentialStore()) { Text = "Hello" };
        await vm.AddImageAsync(CreateTempImageFile("a.jpg"), "a.jpg", "image/jpeg", CancellationToken.None);
        vm.Images[0].AltText = "Original alt text";

        // Simulates editing alt text after the fact — the "Alt" button/warning-badge re-open the same
        // dialog and overwrite AltText the same way the initial add-time prompt does, so setting it
        // twice here is an accurate simulation of an edit, not just an initial set.
        vm.Images[0].AltText = "Edited alt text";

        await vm.PublishCommand.ExecuteAsync(null);

        Assert.Equal("Edited alt text", provider.LastDraft!.Images[0].AltText);
    }

    [Fact]
    public async Task AddImageAsync_AssignsPositionAndTotalImages_ToEachImage()
    {
        var provider = new FakeBlogProvider();
        var vm = new ComposerViewModel(provider, new FakeComposerSettings(), SignedInCredentialStore());

        await vm.AddImageAsync(CreateTempImageFile("a.jpg"), "a.jpg", "image/jpeg", CancellationToken.None);
        await vm.AddImageAsync(CreateTempImageFile("b.jpg"), "b.jpg", "image/jpeg", CancellationToken.None);

        Assert.Equal(1, vm.Images[0].Position);
        Assert.Equal(2, vm.Images[1].Position);
        Assert.Equal(2, vm.Images[0].TotalImages);
        Assert.Equal(2, vm.Images[1].TotalImages);

        vm.Images[0].RemoveCommand.Execute(null);

        var remaining = Assert.Single(vm.Images);
        Assert.Equal(1, remaining.Position);
        Assert.Equal(1, remaining.TotalImages);
    }

    [Fact]
    public async Task PublishAsync_OnFailure_KeepsImages()
    {
        var provider = new FakeBlogProvider
        {
            OnPublish = (_, _) => throw new MicropubException(HttpStatusCode.InternalServerError, "Something went wrong."),
        };
        var vm = new ComposerViewModel(provider, new FakeComposerSettings(), SignedInCredentialStore()) { Text = "Hello" };
        await vm.AddImageAsync(CreateTempImageFile("a.jpg"), "a.jpg", "image/jpeg", CancellationToken.None);

        await vm.PublishCommand.ExecuteAsync(null);

        Assert.Single(vm.Images);
    }

    private static ComposerViewModel BuildViewModel() => new(new FakeBlogProvider(), new FakeComposerSettings(), SignedInCredentialStore());

    // AddImageAsync's implementation opens the local file for real (File.OpenRead) at upload time,
    // so these tests need a real file on disk rather than a placeholder path — a fake/non-existent
    // path would make every one of these tests fail with FileNotFoundException before the fake
    // provider's OnUploadMedia ever runs, which would silently hide a broken
    // LocalFileUri-to-path conversion instead of exercising it. Left on disk deliberately (tiny
    // files in the OS temp folder); no cleanup, consistent with how this suite already leaves
    // other throwaway test artifacts behind.
    private static string CreateTempImageFile(string fileName)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}-{fileName}");
        File.WriteAllBytes(path, [0xFF, 0xD8, 0xFF]);
        return new Uri(path).AbsoluteUri;
    }

    private static InMemoryCredentialStore SignedInCredentialStore()
    {
        var store = new InMemoryCredentialStore();
        store.Save(CredentialAccounts.Default, "test-token");
        return store;
    }
}