using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Transom.App.ViewModels;

/// <summary>One image in the composer's tray: local file, upload state, alt text. No WinUI types
/// (CLAUDE.md Rule 5) — <see cref="LocalFileUri"/> is a plain `file://` URI string so the view
/// binds it through the same `UrlToImageSourceConverter` M1 uses for remote avatar URLs (both are
/// just absolute URIs `BitmapImage` can load).</summary>
public sealed partial class ComposerImageViewModel : ObservableObject
{
    private readonly Func<ComposerImageViewModel, CancellationToken, Task> _upload;
    private readonly Action<ComposerImageViewModel> _remove;
    private readonly Action<ComposerImageViewModel> _moveLeft;
    private readonly Action<ComposerImageViewModel> _moveRight;

    public string LocalFileUri { get; }
    public string FileName { get; }
    public string ContentType { get; }

    /// <summary>Canceled when the image is removed, so an in-flight upload doesn't keep running
    /// or resurrect a removed tile when it completes (Review Focus #4).</summary>
    public CancellationTokenSource UploadCancellation { get; } = new();

    public string? UploadedUrl { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowAltTextWarning))]
    private string? _altText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUploading))]
    [NotifyPropertyChangedFor(nameof(IsFailed))]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    private ComposerImageStatus _status = ComposerImageStatus.Pending;

    [ObservableProperty]
    private double _uploadProgress;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AltTextAutomationName))]
    private int _position;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AltTextAutomationName))]
    private int _totalImages;

    /// <summary>SPEC.md §7: "empty alt text shows a gentle warning, never blocks."</summary>
    public bool ShowAltTextWarning => string.IsNullOrWhiteSpace(AltText);

    /// <summary>Accessible name for the tray's "Alt" button and the warning badge, kept current via
    /// <see cref="Position"/>/<see cref="TotalImages"/> so a screen reader user can tell which tile
    /// they're about to edit (both are re-numbered by <c>ComposerViewModel.RenumberImages</c>
    /// whenever the tray's contents change).</summary>
    public string AltTextAutomationName => $"Edit alt text for image {Position} of {TotalImages}";

    public bool IsUploading => Status == ComposerImageStatus.Uploading;

    public bool IsFailed => Status == ComposerImageStatus.Failed;

    public ComposerImageViewModel(
        string localFileUri, string fileName, string contentType,
        Func<ComposerImageViewModel, CancellationToken, Task> upload,
        Action<ComposerImageViewModel> remove,
        Action<ComposerImageViewModel> moveLeft,
        Action<ComposerImageViewModel> moveRight)
    {
        LocalFileUri = localFileUri;
        FileName = fileName;
        ContentType = contentType;
        _upload = upload;
        _remove = remove;
        _moveLeft = moveLeft;
        _moveRight = moveRight;
    }

    private bool CanRetry() => Status == ComposerImageStatus.Failed;

    [RelayCommand(CanExecute = nameof(CanRetry))]
    private Task RetryAsync() => _upload(this, UploadCancellation.Token);

    [RelayCommand]
    private void Remove() => _remove(this);

    [RelayCommand]
    private void MoveLeft() => _moveLeft(this);

    [RelayCommand]
    private void MoveRight() => _moveRight(this);

    internal void SetUploaded(string url)
    {
        UploadedUrl = url;
        Status = ComposerImageStatus.Uploaded;
        ErrorMessage = null;
    }

    internal void SetFailed(string message)
    {
        Status = ComposerImageStatus.Failed;
        ErrorMessage = message;
    }
}