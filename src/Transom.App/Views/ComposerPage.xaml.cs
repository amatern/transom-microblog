using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

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
            await AddFileAsync(file);
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
            await AddFileAsync(file);
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
                await AddFileAsync(file);
            }
        }
        else if (content.Contains(StandardDataFormats.Bitmap))
        {
            e.Handled = true;
            var bitmapRef = await content.GetBitmapAsync();
            using var stream = await bitmapRef.OpenReadAsync();
            await AddClipboardImageAsync(stream);
        }
    }

    private async Task AddFileAsync(StorageFile file)
    {
        var contentType = ImageContentTypes.FromFileExtension(file.FileType);
        if (contentType is null)
        {
            return;
        }

        using var readStream = await file.OpenStreamForReadAsync();
        var processed = await _imageProcessor.ProcessAsync(readStream, contentType, ImageProcessingOptions.Default, CancellationToken.None);
        await AddProcessedImageAsync(processed);
    }

    private async Task AddClipboardImageAsync(IRandomAccessStreamWithContentType stream)
    {
        var contentType = string.IsNullOrEmpty(stream.ContentType) ? ImageContentTypes.Png : stream.ContentType;
        using var netStream = stream.AsStreamForRead();
        var processed = await _imageProcessor.ProcessAsync(netStream, contentType, ImageProcessingOptions.Default, CancellationToken.None);
        await AddProcessedImageAsync(processed);
    }

    private async Task AddProcessedImageAsync(ProcessedImage processed)
    {
        var folder = await ApplicationData.Current.TemporaryFolder.CreateFolderAsync("composer-images", CreationCollisionOption.OpenIfExists);
        var file = await folder.CreateFileAsync(processed.FileName, CreationCollisionOption.GenerateUniqueName);
        using (var destination = await file.OpenStreamForWriteAsync())
        {
            await processed.Content.CopyToAsync(destination);
        }

        // AddImageAsync (Task 8) silently declines to add the image once the SPEC.md §7 10-image
        // cap is hit (it sets ErrorMessage and returns without touching Images), so only prompt
        // for alt text when an image actually landed in the tray — otherwise Images[^1] would be
        // the wrong (pre-existing) image and this would wrongly re-prompt for its alt text.
        var countBeforeAdd = ViewModel.Images.Count;
        await ViewModel.AddImageAsync(new Uri(file.Path).AbsoluteUri, processed.FileName, processed.ContentType, CancellationToken.None);
        if (ViewModel.Images.Count > countBeforeAdd)
        {
            await PromptForAltTextAsync(ViewModel.Images[^1]);
        }
    }

    private async Task PromptForAltTextAsync(ComposerImageViewModel image)
    {
        var textBox = new TextBox
        {
            PlaceholderText = "Describe this image for people using a screen reader",
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

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            image.AltText = textBox.Text;
        }
    }
}