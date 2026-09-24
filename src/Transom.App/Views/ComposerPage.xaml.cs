using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

using Transom.App.ViewModels;

using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Core;

namespace Transom.App.Views;

public sealed partial class ComposerPage : Page
{
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
    // PublishAccelerator_Invoked. Intercept it here instead: mark it handled up front so no
    // newline is inserted, then run the same publish logic as the accelerator.
    private void ComposerTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
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
}