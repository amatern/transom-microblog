using CommunityToolkit.Mvvm.ComponentModel;

using Transom.Core.Models;

namespace Transom.App.Services;

/// <summary>
/// The signed-in Micro.blog account's display profile, shared as a DI singleton so
/// <c>SettingsViewModel</c> (which populates it on Verify/Sign out) and the app shell's
/// NavigationView footer (which displays it) observe the same state instead of each keeping
/// their own copy. Registering it as a singleton, not one per view model, is what makes the
/// footer update immediately without navigating back to Settings or restarting the app.
/// </summary>
public sealed partial class AccountStateService : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSignedIn))]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string? _accountName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSignedIn))]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string? _accountUsername;

    [ObservableProperty]
    private string? _avatarUrl;

    /// <summary>True once a profile has been populated by <see cref="SetProfile"/>. Username, not
    /// name, is the signal: <c>AccountClient</c>'s config-only fallback (SPEC.md §6.1) returns an
    /// empty username but never a null one, so this still reports signed-in when only a token, no
    /// profile, could be confirmed.</summary>
    public bool IsSignedIn => AccountUsername is not null;

    public string DisplayName => IsSignedIn
        ? (!string.IsNullOrEmpty(AccountName)
            ? AccountName
            : (!string.IsNullOrEmpty(AccountUsername) ? $"@{AccountUsername}" : "Signed in"))
        : "Signed out";

    public void SetProfile(AccountInfo account)
    {
        AccountName = account.Name;
        AccountUsername = account.Username;
        AvatarUrl = account.Avatar;
    }

    public void Clear()
    {
        AccountName = null;
        AccountUsername = null;
        AvatarUrl = null;
    }
}