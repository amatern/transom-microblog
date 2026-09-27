using System.Net;

using Transom.Core.MicroBlog;

namespace Transom.App.Services;

/// <summary>Maps a Micro.blog failure to the plain-English message SPEC.md §4.2 calls for.
/// Extracted from <c>ComposerViewModel</c> (prompt 12 added these three branches for Publish;
/// prompt 15/M2 needs the exact same wording for a failed image upload) so both paths stay in
/// sync instead of duplicating the branch.</summary>
public static class ComposerErrorMessages
{
    public static string Describe(Exception exception) => exception switch
    {
        MicropubException { StatusCode: null } => "You appear to be offline. Check your connection and try again.",
        MicropubException { StatusCode: HttpStatusCode.Unauthorized } => "Your app token was rejected. Paste a new one in Settings.",
        MicropubException ex => ex.Message,
        _ => exception.Message,
    };
}