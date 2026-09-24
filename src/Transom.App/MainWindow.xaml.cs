using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

using Transom.App.Converters;
using Transom.App.Services;
using Transom.App.ViewModels;
using Transom.App.Views;

namespace Transom.App;

public sealed partial class MainWindow : Window
{
    // Window isn't a FrameworkElement, so x:Bind can't be used here (the WinUI XAML compiler's
    // generated Bindings.SetConverterLookupRoot requires one) — the account footer is updated
    // imperatively instead, from the same AccountStateService singleton SettingsViewModel writes
    // to, so Verify/Sign out are reflected here immediately without navigating or restarting.
    private readonly AccountStateService _account;
    private readonly UrlToImageSourceConverter _avatarConverter = new();

    public MainWindow()
    {
        _account = App.Host.Services.GetRequiredService<AccountStateService>();
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");

        ContentFrame.Navigate(typeof(ComposerPage));

        _account.PropertyChanged += (_, _) => UpdateAccountFooter();
        UpdateAccountFooter();

        // Restore signed-in state at launch, not just on first visiting Settings — otherwise an
        // already-signed-in user sees "Signed out" in the footer until they open Settings once.
        App.Host.Services.GetRequiredService<SettingsViewModel>().LoadCommand.Execute(null);
    }

    private void UpdateAccountFooter()
    {
        AccountFooterName.Text = _account.DisplayName;
        AccountFooterAvatar.ProfilePicture = (ImageSource?)_avatarConverter.Convert(_account.AvatarUrl!, typeof(ImageSource), null!, null!);
        AutomationProperties.SetName(AccountFooterItem, _account.DisplayName);
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        var tag = (args.InvokedItemContainer as NavigationViewItem)?.Tag as string;
        ContentFrame.Navigate(tag switch
        {
            "Settings" => typeof(SettingsPage),
            _ => typeof(ComposerPage),
        });
    }
}