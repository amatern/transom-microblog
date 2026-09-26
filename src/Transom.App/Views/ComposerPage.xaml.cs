using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

using Transom.App.Services;
using Transom.App.ViewModels;
using Transom.Core.Media;

using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;

namespace Transom.App.Views;

public sealed partial class ComposerPage : Page
{
    private readonly IImageProcessor _imageProcessor = App.Host.Services.GetRequiredService<IImageProcessor>();

    public ComposerViewModel ViewModel { get; }

    public ComposerPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<ComposerViewModel>();
        InitializeComponent();
    }

    private void PublishAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ViewModel.PublishCommand.CanExecute(null))
        {
            ViewModel.PublishCommand.Execute(null);
        }
        args.Handled = true;
    }

    // The Title and Post-text TextBoxes handle Enter themselves (AcceptsReturn on the body box,
    // and TextBox reserves the Enter key in general) before the page-level KeyboardAccelerator
    // ever sees it, so Ctrl+Enter while focus is in either box never reached
    // PublishAccelerator_Invoked. Intercept it here instead. This must be PreviewKeyDown, not
    // KeyDown: TextBox's own newline-insertion runs as class handling of KeyDown, which fires
    // before an instance KeyDown handler on the same TextBox ever sees the event, so e.Handled
    // set there is too late. PreviewKeyDown tunnels ahead of that and can suppress it.
    private void ComposerTextBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        var ctrlState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
        if ((ctrlState & CoreVirtualKeyStates.Down) != CoreVirtualKeyStates.Down)
        {
            return;
        }

        e.Handled = true;
        if (ViewModel.PublishCommand.CanExecute(null))
        {
            ViewModel.PublishCommand.Execute(null);
        }
    }

    private void CopyLinkButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.PublishedUri is not { } uri)
        {
            return;
        }

        var package = new DataPackage();
        package.SetText(uri.AbsoluteUri);
        Clipboard.SetContent(package);
    }

    private async void AddImageButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        foreach (var extension in new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".heic" })
        {
            picker.FileTypeFilter.Add(extension);
        }

        var files = await picker.PickMultipleFilesAsync();
        foreach (var file in files)
        {
            await AddFileAsync(file, CancellationToken.None);
        }
    }

    private void ComposerRoot_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private async void ComposerRoot_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var items = await e.DataView.GetStorageItemsAsync();
        foreach (var file in items.OfType<StorageFile>())
        {
            await AddFileAsync(file, CancellationToken.None);
        }
    }

    private async void BodyBox_Paste(object sender, TextControlPasteEventArgs e)
    {
        var content = Clipboard.GetContent();
        if (content.Contains(StandardDataFormats.StorageItems))
        {
            e.Handled = true;
            var items = await content.GetStorageItemsAsync();
            foreach (var file in items.OfType<StorageFile>())
            {
                await AddFileAsync(file, CancellationToken.None);
            }
        }
        else if (content.Contains(StandardDataFormats.Bitmap))
        {
            e.Handled = true;
            var bitmapRef = await content.GetBitmapAsync();
            using var stream = await bitmapRef.OpenReadAsync();
            await AddClipboardImageAsync(stream, CancellationToken.None);
        }
    }

    private async Task AddFileAsync(StorageFile file, CancellationToken cancellationToken)
    {
        ViewModel.AddImageErrorMessage = null;

        var contentType = ImageContentTypes.FromFileExtension(file.FileType);
        if (contentType is null)
        {
            return;
        }

        using var readStream = await file.OpenStreamForReadAsync();
        ProcessedImage processed;
        try
        {
            processed = await _imageProcessor.ProcessAsync(readStream, contentType, ImageProcessingOptions.Default, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // WicImageProcessor deliberately wraps WIC/codec failures (corrupt file, missing HEIC
            // codec, etc.) into a friendly InvalidOperationException. This handler is `async void`
            // at the top of the call chain, so an uncaught exception here is fatal to the whole app
            // (CLAUDE.md Rule 6: never lose the user's draft). Surface it the same way every other
            // failure in this app is surfaced instead of propagating.
            ViewModel.AddImageErrorMessage = ComposerErrorMessages.Describe(ex);
            return;
        }

        await AddProcessedImageAsync(processed, cancellationToken);
    }

    private async Task AddClipboardImageAsync(IRandomAccessStreamWithContentType stream, CancellationToken cancellationToken)
    {
        ViewModel.AddImageErrorMessage = null;

        var contentType = string.IsNullOrEmpty(stream.ContentType) ? ImageContentTypes.Png : stream.ContentType;
        using var netStream = stream.AsStreamForRead();
        ProcessedImage processed;
        try
        {
            processed = await _imageProcessor.ProcessAsync(netStream, contentType, ImageProcessingOptions.Default, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ViewModel.AddImageErrorMessage = ComposerErrorMessages.Describe(ex);
            return;
        }

        await AddProcessedImageAsync(processed, cancellationToken);
    }

    private async Task AddProcessedImageAsync(ProcessedImage processed, CancellationToken cancellationToken)
    {
        var folder = await ApplicationData.Current.TemporaryFolder.CreateFolderAsync("composer-images", CreationCollisionOption.OpenIfExists);
        var file = await folder.CreateFileAsync(processed.FileName, CreationCollisionOption.GenerateUniqueName);
        using (var destination = await file.OpenStreamForWriteAsync())
        {
            await processed.Content.CopyToAsync(destination, cancellationToken);
        }

        // AddImageAsync (Task 8) silently declines to add the image once the SPEC.md §7 10-image
        // cap is hit (it sets ErrorMessage and returns without touching Images), so only prompt
        // for alt text when an image actually landed in the tray — otherwise Images[^1] would be
        // the wrong (pre-existing) image and this would wrongly re-prompt for its alt text.
        var countBeforeAdd = ViewModel.Images.Count;
        await ViewModel.AddImageAsync(new Uri(file.Path).AbsoluteUri, processed.FileName, processed.ContentType, cancellationToken);
        if (ViewModel.Images.Count > countBeforeAdd)
        {
            await PromptForAltTextAsync(ViewModel.Images[^1], cancellationToken);
        }
    }

    private async Task PromptForAltTextAsync(ComposerImageViewModel image, CancellationToken cancellationToken)
    {
        var textBox = new TextBox
        {
            PlaceholderText = "Describe this image for people using a screen reader",
            Text = image.AltText ?? string.Empty,
        };
        AutomationProperties.SetName(textBox, "Alt text");
        var dialog = new ContentDialog
        {
            Title = "Add alt text",
            Content = textBox,
            PrimaryButtonText = "Save",
            CloseButtonText = "Skip",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        var result = await dialog.ShowAsync().AsTask(cancellationToken);
        if (result == ContentDialogResult.Primary)
        {
            image.AltText = textBox.Text;
        }
    }

    // Shared by the tray tile's "Alt" button and the warning-badge button (both live inside the
    // ItemsRepeater's x:Bind-only ItemTemplate). x:Bind never sets DataContext on the elements it
    // binds — that's a {Binding}-only mechanism — so reading sender's DataContext here always got
    // null and this handler silently no-opped (see CLAUDE.md's x:Bind/DataContext note). Instead
    // the XAML binds `Tag="{x:Bind}"` on both buttons, which assigns the tile's own
    // ComposerImageViewModel to the plain Tag dependency property at compile time; read that
    // instead. This is used here (rather than a Command binding) because opening a ContentDialog
    // needs code-behind (view models can't reference WinUI types, CLAUDE.md Rule 5).
    private async void EditAltTextButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not ComposerImageViewModel image)
        {
            // Defensive: Tag is bound via x:Bind and should always resolve to the clicked tile's
            // image. If it somehow doesn't, don't fail silently — that's exactly the bug this
            // replaces (the old DataContext-based lookup silently no-opped instead of ever reaching
            // PromptForAltTextAsync, since x:Bind-only DataTemplates never populate DataContext;
            // see CLAUDE.md's x:Bind/DataContext note and Rule 11 on silent failures).
            ViewModel.AddImageErrorMessage = "Couldn't open alt text for that image. Please try again.";
            return;
        }

        try
        {
            await PromptForAltTextAsync(image, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ViewModel.AddImageErrorMessage = ComposerErrorMessages.Describe(ex);
        }
    }
}