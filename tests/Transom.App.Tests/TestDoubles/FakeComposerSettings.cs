using Transom.App.Services;

namespace Transom.App.Tests.TestDoubles;

internal sealed class FakeComposerSettings : IComposerSettings
{
    public bool PostAsDraft { get; set; }
}