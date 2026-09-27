using System.Text;

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

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Create a copy of the request with buffered content so tests can read it later.
        var copy = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy,
        };

        // Copy headers
        foreach (var header in request.Headers)
        {
            copy.Headers.Add(header.Key, header.Value);
        }

        // Buffer and preserve content
        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            copy.Content = new ByteArrayContent(bytes);

            // Preserve the original content type if it exists
            if (request.Content.Headers.ContentType is not null)
            {
                copy.Content.Headers.ContentType = request.Content.Headers.ContentType;
            }

            // Copy other content headers
            foreach (var header in request.Content.Headers.Where(h => h.Key != "Content-Type"))
            {
                copy.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        LastRequest = copy;
        return _respond(request);
    }
}