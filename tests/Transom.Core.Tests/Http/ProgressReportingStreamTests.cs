using Transom.Core.Http;

namespace Transom.Core.Tests.Http;

public class ProgressReportingStreamTests
{
    [Fact]
    public async Task ReadAsync_ReportsIncreasingProgress_EndingAtOne()
    {
        var data = new byte[10_000];
        var reported = new List<double>();
        using var source = new MemoryStream(data);

        // Progress<T> always marshals Report() through a captured SynchronizationContext via
        // Post(), which runs asynchronously even when no ambient context exists (the default
        // context posts to the thread pool) — asserting on `reported` right after the read loop
        // would race the last post. IProgress<double> itself makes no such promise, so a direct,
        // synchronous implementation is what this test actually needs to verify the stream's own
        // reporting order deterministically.
        using var stream = new ProgressReportingStream(source, data.Length, new SynchronousProgress<double>(reported.Add));

        var buffer = new byte[4096];
        while (await stream.ReadAsync(buffer) > 0)
        {
        }

        Assert.NotEmpty(reported);
        Assert.Equal(1.0, reported[^1]);
        Assert.True(reported.SequenceEqual(reported.OrderBy(v => v)), "progress must never go backwards");
    }

    [Fact]
    public async Task ReadAsync_UnknownLength_ReportsOneAfterEachRead_ButNeverThrows()
    {
        using var source = new MemoryStream(new byte[100]);
        using var stream = new ProgressReportingStream(source, length: -1, progress: new Progress<double>());

        var buffer = new byte[100];
        var read = await stream.ReadAsync(buffer);

        Assert.Equal(100, read);
    }

    private sealed class SynchronousProgress<T> : IProgress<T>
    {
        private readonly Action<T> _report;

        public SynchronousProgress(Action<T> report) => _report = report;

        public void Report(T value) => _report(value);
    }
}