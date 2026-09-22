using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Transom.App.Services;
using Transom.Core.Credentials;
using Transom.Core.Models;
using Transom.Core.Providers;

namespace Transom.App.ViewModels;

/// <summary>Composer state and publish logic. No WinUI types (CLAUDE.md Rule 5) — unit tested in
/// <c>Transom.App.Tests</c>.</summary>
public sealed partial class ComposerViewModel : ObservableObject
{
    /// <summary>SPEC.md §4.2: the Title field appears once the post exceeds Micro.blog's
    /// short/long post threshold.</summary>
    private const int TitleThreshold = 300;

    private readonly IBlogProvider _provider;
    private readonly IComposerSettings _settings;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CharacterCount))]
    [NotifyPropertyChangedFor(nameof(ShowTitleField))]
    [NotifyCanExecuteChangedFor(nameof(PublishCommand))]
    private string _text = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PublishCommand))]
    private bool _isPublishing;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _publishedUrl;

    public int CharacterCount => Text.Length;

    public bool ShowTitleField => Text.Length > TitleThreshold;

    public bool IsSignedIn { get; }

    public bool ShowSignInHint => !IsSignedIn;

    public ComposerViewModel(IBlogProvider provider, IComposerSettings settings, ICredentialStore credentialStore)
    {
        _provider = provider;
        _settings = settings;
        IsSignedIn = credentialStore.TryGet(CredentialAccounts.Default) is not null;
    }

    private bool CanPublish() => !IsPublishing && !string.IsNullOrWhiteSpace(Text) && IsSignedIn;

    [RelayCommand(CanExecute = nameof(CanPublish))]
    private async Task PublishAsync(CancellationToken cancellationToken)
    {
        IsPublishing = true;
        ErrorMessage = null;
        try
        {
            var draft = new PostDraft(Text, ShowTitleField ? Title : null, _settings.PostAsDraft);
            var result = await _provider.PublishAsync(draft, cancellationToken).ConfigureAwait(true);
            PublishedUrl = result.Url;
            Text = string.Empty;
            Title = string.Empty;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsPublishing = false;
        }
    }
}