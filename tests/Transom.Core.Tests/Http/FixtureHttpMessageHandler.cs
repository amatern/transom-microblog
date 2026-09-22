namespace Transom.Core.Tests.Http;

/// <summary>
/// A fake <see cref="HttpMessageHandler"/> whose response is produced by a caller-supplied
/// delegate, so each test builds the exact <see cref="HttpResponseMessage"/> (status, headers,
/// body) it needs from a fixture file or an in-line string.
/// </summary>
internal sealed class FixtureHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public HttpRequestMessage? LastRequest { get; private set; }

    public FixtureHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _respond = respond;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(_respond(request));
    }
}