using Transom.App.ViewModels;

namespace Transom.App.Tests.ViewModels;

public class ComposerImageViewModelTests
{
    private static ComposerImageViewModel Build(
        Func<ComposerImageViewModel, CancellationToken, Task>? upload = null,
        Action<ComposerImageViewModel>? remove = null,
        Action<ComposerImageViewModel>? moveLeft = null,
        Action<ComposerImageViewModel>? moveRight = null)
        => new("file:///C:/temp/photo.jpg", "photo.jpg", "image/jpeg",
            upload ?? ((_, _) => Task.CompletedTask),
            remove ?? (_ => { }),
            moveLeft ?? (_ => { }),
            moveRight ?? (_ => { }));

    [Fact]
    public void NewImage_StartsPending_WithAltTextWarningShown()
    {
        var image = Build();

        Assert.Equal(ComposerImageStatus.Pending, image.Status);
        Assert.True(image.ShowAltTextWarning);
    }

    [Fact]
    public void SettingAltText_HidesWarning()
    {
        var image = Build();

        image.AltText = "A red bicycle";

        Assert.False(image.ShowAltTextWarning);
    }

    [Fact]
    public void SetUploaded_StoresUrlAndClearsError()
    {
        var image = Build();

        image.SetUploaded("https://cdn.micro.blog/uploads/photo.jpg");

        Assert.Equal(ComposerImageStatus.Uploaded, image.Status);
        Assert.Equal("https://cdn.micro.blog/uploads/photo.jpg", image.UploadedUrl);
        Assert.Null(image.ErrorMessage);
    }

    [Fact]
    public void SetFailed_SetsErrorAndEnablesRetry()
    {
        var image = Build();
        Assert.False(image.RetryCommand.CanExecute(null));

        image.SetFailed("You appear to be offline. Check your connection and try again.");

        Assert.Equal(ComposerImageStatus.Failed, image.Status);
        Assert.Equal("You appear to be offline. Check your connection and try again.", image.ErrorMessage);
        Assert.True(image.RetryCommand.CanExecute(null));
    }

    [Fact]
    public async Task RetryCommand_InvokesUploadDelegate()
    {
        var invoked = false;
        var image = Build(upload: (_, _) => { invoked = true; return Task.CompletedTask; });
        image.SetFailed("failed");

        await image.RetryCommand.ExecuteAsync(null);

        Assert.True(invoked);
    }

    [Fact]
    public void RemoveCommand_InvokesRemoveDelegate()
    {
        ComposerImageViewModel? removed = null;
        var image = Build(remove: img => removed = img);

        image.RemoveCommand.Execute(null);

        Assert.Same(image, removed);
    }

    [Fact]
    public void MoveLeftCommand_InvokesMoveLeftDelegate()
    {
        ComposerImageViewModel? moved = null;
        var image = Build(moveLeft: img => moved = img);

        image.MoveLeftCommand.Execute(null);

        Assert.Same(image, moved);
    }

    [Fact]
    public void SetFailed_PutsTheReasonInTheTileAccessibleName()
    {
        var image = Build();
        var changed = new List<string?>();
        image.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        image.SetFailed("You appear to be offline. Check your connection and try again.");

        Assert.Equal("photo.jpg, upload failed: You appear to be offline. Check your connection and try again.", image.TileAutomationName);
        Assert.Contains(nameof(ComposerImageViewModel.TileAutomationName), changed);
        Assert.Equal("You appear to be offline. Check your connection and try again.", image.FailureText);
    }

    [Fact]
    public void TileAccessibleName_IsJustTheFileName_UnlessFailed()
    {
        var image = Build();
        Assert.Equal("photo.jpg", image.TileAutomationName);
        Assert.Equal(string.Empty, image.FailureText);

        image.SetFailed("Upload failed on the server.");
        image.SetUploaded("https://cdn.micro.blog/uploads/photo.jpg");

        Assert.Equal("photo.jpg", image.TileAutomationName);
        Assert.Equal(string.Empty, image.FailureText);
    }
}