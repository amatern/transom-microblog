using System.Net;

namespace Transom.Core.MicroBlog;

/// <summary>
/// Thrown when a Micro.blog API call fails. The message never includes the request's
/// Authorization token (CLAUDE.md Rule 1) — it carries only the status code and, when parseable,
/// the response's <c>error</c> field.
/// </summary>
public sealed class MicropubException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public MicropubException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}