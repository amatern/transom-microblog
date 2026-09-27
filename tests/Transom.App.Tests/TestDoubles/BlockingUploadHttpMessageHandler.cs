using System.Net;
using System.Text;

namespace Transom.App.Tests.TestDoubles;

/// <summary>A fake <see cref="HttpMessageHandler"/> for driving the real <c>MicroBlogProvider</c> →
/// <c>MicropubClient</c> → <c>HttpClient</c> chain without a network call (CLAUDE.md Rule 2).
/// Answers the Micropub config query, then answers each media upload with the next scripted
/// <see cref="UploadOutcome"/>. <see cref="UploadOutcome.Block"/> (also the default once the script
/// runs out) holds the upload open until the request's own cancellation token fires — what a large
/// upload looks like when the user removes its tile, so the cancellation surfaces the way
/// <c>HttpClient</c> really surfaces it.</summary>
internal sealed class BlockingUploadHttpMessageHandler : HttpMessageHandler
{
    public enum UploadOutcome
    {
        Block,
        Succeed,
        Fail,
    }

    private const string ConfigJson = """{ "media-endpoint": "https://micro.blog/micropub/media" }""";

    private readonly Queue<UploadOutcome> _outcomes;
    private readonly TaskCompletionSource _uploadBlocked = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public BlockingUploadHttpMessageHandler(params UploadOutcome[] outcomes)
    {
        _outcomes = new Queue<UploadOutcome>(outcomes);
    }

    /// <summary>Completes once a media upload has reached the handler and is being held open.</summary>
    public Task UploadBlocked => _uploadBlocked.Task;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri!.Query.Contains("q=config"))
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ConfigJson, Encoding.UTF8, "application/json"),
            };
        }

        var outcome = _outcomes.TryDequeue(out var next) ? next : UploadOutcome.Block;
        switch (outcome)
        {
            case UploadOutcome.Succeed:
                var created = new HttpResponseMessage(HttpStatusCode.Created);
                created.Headers.Location = new Uri($"https://cdn.micro.blog/uploads/{Guid.NewGuid()}.jpg");
                return created;
            case UploadOutcome.Fail:
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("""{ "error": "Upload failed on the server." }""", Encoding.UTF8, "application/json"),
                };
            default:
                _uploadBlocked.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException("Unreachable: the delay above only ends by cancellation.");
        }
    }
}