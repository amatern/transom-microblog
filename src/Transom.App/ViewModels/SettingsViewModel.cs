using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Transom.App.Services;
using Transom.Core.Credentials;
using Transom.Core.MicroBlog;
using Transom.Core.Models;

namespace Transom.App.ViewModels;

/// <summary>Token entry, verification and profile display (SPEC.md §4.4). No WinUI types
/// (CLAUDE.md Rule 5) — unit tested in <c>Transom.App.Tests</c>. The profile itself lives in
/// <see cref="Services.AccountStateService"/> (a DI singleton), not on this view model, so the app
/// shell's NavigationView footer sees the same state instead of a stale copy.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AccountClient _accountClient;
    private readonly ICredentialStore _credentialStore;

    public AccountStateService Account { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerifyCommand))]
    private string _tokenInput = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerifyCommand))]
    private bool _isVerifying;

    [ObservableProperty]
    private string? _errorMessage;

    public SettingsViewModel(AccountClient accountClient, ICredentialStore credentialStore, AccountStateService account)
    {
        _accountClient = accountClient;
        _credentialStore = credentialStore;
        Account = account;
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
        Account.Clear();
        ErrorMessage = null;
    }

    private void PopulateProfile(AccountInfo account) => Account.SetProfile(account);
}