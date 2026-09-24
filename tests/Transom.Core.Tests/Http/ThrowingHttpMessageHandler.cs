namespace Transom.Core.Tests.Http;

/// <summary>A fake <see cref="HttpMessageHandler"/> that throws instead of responding, so tests
/// can simulate connectivity failures (no route to host, DNS failure, timeout) without a real
/// network call (CLAUDE.md Rule 2).</summary>
internal sealed class ThrowingHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<Exception> _createException;

    public ThrowingHttpMessageHandler(Func<Exception> createException)
    {
        _createException = createException;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw _createException();
    }
}