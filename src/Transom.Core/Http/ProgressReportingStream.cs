namespace Transom.Core.Http;

/// <summary>Wraps a read-only stream to report fractional read progress. <c>HttpClient</c> reads a
/// request's content stream in chunks while serializing it, so wrapping the file part of a
/// multipart upload in this is how per-image upload progress (SPEC.md §4.2) is surfaced without
/// any WinRT dependency — this is plain <c>Stream</c> plumbing, unit-tested with a
/// <c>MemoryStream</c>.</summary>
public sealed class ProgressReportingStream : Stream
{
    private readonly Stream _inner;
    private readonly long _length;
    private readonly IProgress<double>? _progress;
    private long _bytesRead;

    public ProgressReportingStream(Stream inner, long length, IProgress<double>? progress)
    {
        _inner = inner;
        _length = length;
        _progress = progress;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _length;
    public override long Position
    {
        get => _bytesRead;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = _inner.Read(buffer, offset, count);
        Report(read);
        return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        // Delegate to Memory<byte> overload
        return await ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        Report(read);
        return read;
    }

    private void Report(int bytesJustRead)
    {
        if (bytesJustRead <= 0)
        {
            return;
        }

        _bytesRead += bytesJustRead;
        _progress?.Report(_length > 0 ? Math.Min(1.0, (double)_bytesRead / _length) : 1.0);
    }

    public override void Flush() => _inner.Flush();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
