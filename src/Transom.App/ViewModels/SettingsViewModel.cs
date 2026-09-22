using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Transom.Core.Credentials;
using Transom.Core.MicroBlog;

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
    private string? _accountUsername;

    [ObservableProperty]
    private string? _avatarUrl;

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
            AccountName = account.Name;
            AccountUsername = account.Username;
            AvatarUrl = account.Avatar;
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
}