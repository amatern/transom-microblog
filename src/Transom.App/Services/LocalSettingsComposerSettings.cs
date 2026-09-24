using Windows.Storage;

namespace Transom.App.Services;

public sealed class LocalSettingsComposerSettings : IComposerSettings
{
    private const string PostAsDraftKey = "ComposerPostAsDraft";

#if DEBUG
    private const bool DefaultPostAsDraft = true;
#else
    private const bool DefaultPostAsDraft = false;
#endif

    public bool PostAsDraft
    {
        get => ApplicationData.Current.LocalSettings.Values.TryGetValue(PostAsDraftKey, out var value) && value is bool stored
            ? stored
            : DefaultPostAsDraft;
        set => ApplicationData.Current.LocalSettings.Values[PostAsDraftKey] = value;
    }
}