using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

using Transom.App.Services;
using Transom.App.ViewModels;

namespace Transom.App.Views;

public sealed partial class SettingsPage : Page
{
    private readonly IComposerSettings _composerSettings;

    public SettingsViewModel ViewModel { get; }

    public bool PostAsDraft
    {
        get => _composerSettings.PostAsDraft;
        set => _composerSettings.PostAsDraft = value;
    }

    public SettingsPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<SettingsViewModel>();
        _composerSettings = App.Host.Services.GetRequiredService<IComposerSettings>();
        InitializeComponent();
        Loaded += (_, _) => ViewModel.LoadCommand.Execute(null);
    }
}