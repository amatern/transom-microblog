using System.Net;

namespace Transom.Core.MicroBlog;

/// <summary>
/// Thrown when a Micro.blog API call fails. The message never includes the request's
/// Authorization token (CLAUDE.md Rule 1) — it carries only the status code and, when parseable,
/// the response's <c>error</c> field. <see cref="StatusCode"/> is <c>null</c> when the request
/// never reached the server at all (DNS failure, no connection, timeout) — see
/// <see cref="MicropubClient"/>, which wraps <see cref="HttpRequestException"/> and
/// <see cref="TaskCanceledException"/> into this type so callers only ever handle one exception
/// type for Micro.blog failures.
/// </summary>
public sealed class MicropubException : Exception
{
    public HttpStatusCode? StatusCode { get; }

    public MicropubException(HttpStatusCode? statusCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}