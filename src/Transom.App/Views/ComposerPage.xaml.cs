using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

using Transom.App.ViewModels;

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
}