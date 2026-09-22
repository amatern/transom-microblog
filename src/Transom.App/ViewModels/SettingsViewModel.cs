using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Transom.Core.Credentials;
using Transom.Core.MicroBlog;
using Transom.Core.Models;

namespace Transom.App.ViewModels;

/// <summary>Token entry, verification and profile display (SPEC.md §4.4). No WinUI types
/// (CLAUDE.md Rule 5) — unit tested in <c>Transom.App.Tests</c>.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AccountClient _accountClient;
    private readonly ICredentialStore _credentialStore;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerifyCommand))]
    private string _tokenInput = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerifyCommand))]
    private bool _isVerifying;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _accountName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSignedIn))]
    private string? _accountUsername;

    [ObservableProperty]
    private string? _avatarUrl;

    public bool IsSignedIn => AccountUsername is not null;

    public SettingsViewModel(AccountClient accountClient, ICredentialStore credentialStore)
    {
        _accountClient = accountClient;
        _credentialStore = credentialStore;
    }

    private bool CanVerify() => !IsVerifying && !string.IsNullOrWhiteSpace(TokenInput);

    [RelayCommand(CanExecute = nameof(CanVerify))]
    private async Task VerifyAsync(CancellationToken cancellationToken)
    {
        IsVerifying = true;
        ErrorMessage = null;
        try
        {
            var account = await _accountClient.VerifyAsync(TokenInput, cancellationToken).ConfigureAwait(true);
            _credentialStore.Save(CredentialAccounts.Default, account.Token);
            PopulateProfile(account);
            TokenInput = string.Empty;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsVerifying = false;
        }
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var token = _credentialStore.TryGet(CredentialAccounts.Default);
        if (token is null)
        {
            return;
        }

        IsVerifying = true;
        ErrorMessage = null;
        try
        {
            var account = await _accountClient.VerifyAsync(token, cancellationToken).ConfigureAwait(true);
            _credentialStore.Save(CredentialAccounts.Default, account.Token);
            PopulateProfile(account);
        }
        catch (Exception)
        {
            ErrorMessage = "Couldn't verify your saved token";
        }
        finally
        {
            IsVerifying = false;
        }
    }

    [RelayCommand]
    private void SignOut()
    {
        _credentialStore.Remove(CredentialAccounts.Default);
        AccountName = null;
        AccountUsername = null;
        AvatarUrl = null;
        ErrorMessage = null;
    }

    private void PopulateProfile(AccountInfo account)
    {
        AccountName = account.Name;
        AccountUsername = account.Username;
        AvatarUrl = account.Avatar;
    }
}