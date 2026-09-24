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
    [NotifyPropertyChangedFor(nameof(CharacterCountDisplay))]
    [NotifyPropertyChangedFor(nameof(CharacterCountAutomationName))]
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
    [NotifyPropertyChangedFor(nameof(PublishedUri))]
    [NotifyPropertyChangedFor(nameof(HasPublishedUri))]
    private string? _publishedUrl;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PublishSuccessTitle))]
    private bool _publishedAsDraft;

    public int CharacterCount => Text.Length;

    /// <summary>Compact "N/threshold" label for the character counter, e.g. "42/300".</summary>
    public string CharacterCountDisplay => $"{CharacterCount}/{TitleThreshold}";

    public string CharacterCountAutomationName => $"{CharacterCount} of {TitleThreshold} characters";

    public bool ShowTitleField => Text.Length > TitleThreshold;

    public bool IsSignedIn { get; }

    public bool ShowSignInHint => !IsSignedIn;

    /// <summary>The published or draft-preview URL as a <see cref="Uri"/>, or null when
    /// <see cref="PublishedUrl"/> is missing or not a valid absolute URI (defends against binding
    /// a malformed string straight to a XAML Uri-typed property — see CLAUDE.md Style).</summary>
    public Uri? PublishedUri => Uri.TryCreate(PublishedUrl, UriKind.Absolute, out var uri) ? uri : null;

    public bool HasPublishedUri => PublishedUri is not null;

    public string PublishSuccessTitle => PublishedAsDraft ? "Saved as draft" : "Published";

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
        PublishedUrl = null;
        PublishedAsDraft = false;
        try
        {
            var postAsDraft = _settings.PostAsDraft;
            var draft = new PostDraft(Text, ShowTitleField ? Title : null, postAsDraft);
            var result = await _provider.PublishAsync(draft, cancellationToken).ConfigureAwait(true);

            // SPEC.md §6.2: a draft response carries both `url` (the eventual public URL, which
            // 404s until the post is published for real) and `preview`. Link to preview for drafts.
            PublishedAsDraft = postAsDraft;
            PublishedUrl = postAsDraft && !string.IsNullOrEmpty(result.PreviewUrl) ? result.PreviewUrl : result.Url;
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