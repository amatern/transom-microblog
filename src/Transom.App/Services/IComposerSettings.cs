namespace Transom.App.Services;

/// <summary>Local, per-device composer preferences (SPEC.md §4.4).</summary>
public interface IComposerSettings
{
    /// <summary>
    /// When true, publishes go out with <c>post-status=draft</c> instead of live, so manual
    /// testing against a real account doesn't create public posts. Defaults to <c>true</c> in
    /// Debug builds, <c>false</c> in Release.
    /// </summary>
    bool PostAsDraft { get; set; }
}