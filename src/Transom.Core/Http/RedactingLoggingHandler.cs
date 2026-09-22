using System.Net.Http.Headers;

using Microsoft.Extensions.Logging;

namespace Transom.Core.Http;

/// <summary>
/// A <see cref="DelegatingHandler"/> that logs outgoing requests and incoming status codes
/// without ever writing an <c>Authorization</c> header value to the log.
/// </summary>
public sealed class RedactingLoggingHandler : DelegatingHandler
{
    private readonly ILogger<RedactingLoggingHandler> _logger;

    public RedactingLoggingHandler(ILogger<RedactingLoggingHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("{Method} {Uri} Authorization: {Authorization}", request.Method, request.RequestUri, RedactAuthorization(request.Headers.Authorization));
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("{Method} {Uri} -> {StatusCode}", request.Method, request.RequestUri, (int)response.StatusCode);
        return response;
    }

    internal static string RedactAuthorization(AuthenticationHeaderValue? authorization)
    {
        return authorization is null ? "(none)" : $"{authorization.Scheme} [REDACTED]";
    }
}