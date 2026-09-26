using System.Net.Http.Headers;
using System.Text.Json;

using Transom.Core.Http;
using Transom.Core.Models;

namespace Transom.Core.MicroBlog;

/// <summary>Client for Micro.blog's Micropub endpoints (<c>SPEC.md</c> §6.2).</summary>
public sealed class MicropubClient
{
    private readonly HttpClient _httpClient;

    public MicropubClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<MicropubConfig> GetConfigAsync(string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/micropub?q=config");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        await ThrowIfErrorAsync(response, cancellationToken).ConfigureAwait(false);

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var config = await JsonSerializer.DeserializeAsync(stream, TransomJsonContext.Default.MicropubConfig, cancellationToken).ConfigureAwait(false);
        return config ?? new MicropubConfig();
    }

    public async Task<PublishResult> PublishAsync(string token, PostDraft draft, CancellationToken cancellationToken)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new("h", "entry"),
            new("content", draft.Content),
        };
        if (!string.IsNullOrEmpty(draft.Title))
        {
            form.Add(new("name", draft.Title));
        }
        if (draft.PostAsDraft)
        {
            form.Add(new("post-status", "draft"));
        }
        foreach (var image in draft.Images)
        {
            form.Add(new("photo[]", image.Url));
            form.Add(new("mp-photo-alt[]", image.AltText ?? string.Empty));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/micropub")
        {
            Content = new FormUrlEncodedContent(form),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        await ThrowIfErrorAsync(response, cancellationToken).ConfigureAwait(false);

        // Read the body even when a Location header is present: SPEC.md §6.2 documents Location as
        // the general success signal and `preview` as a body field a draft response carries, and
        // doesn't say the two are mutually exclusive. Returning on Location alone would silently
        // drop `preview` for a server that sends both, which is exactly the "draft links to the
        // eventual public URL" bug this client exists to avoid.
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var body = string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize(json, TransomJsonContext.Default.PublishResponseBody);

        var url = !string.IsNullOrEmpty(body?.Url) ? body.Url : response.Headers.Location?.ToString();
        if (string.IsNullOrEmpty(url))
        {
            throw new MicropubException(response.StatusCode, "Publish succeeded but no post URL was returned.");
        }

        return new PublishResult(url, body?.Preview);
    }

    /// <summary>
    /// Uploads media to the Micropub media endpoint, returning the URL assigned by the server.
    /// Wraps the uploaded stream in <see cref="ProgressReportingStream"/> to report progress without
    /// WinRT dependencies. Callers may pass <c>null</c> for <paramref name="progress"/> if they
    /// don't wish to track upload progress.
    /// </summary>
    public async Task<MediaItem> UploadMediaAsync(string token, string mediaEndpoint, Stream data, string fileName, string contentType, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var length = data.CanSeek ? data.Length : -1;
        using var progressStream = new ProgressReportingStream(data, length, progress);
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(progressStream);
        fileContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType);
        content.Add(fileContent, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, mediaEndpoint) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        await ThrowIfErrorAsync(response, cancellationToken).ConfigureAwait(false);

        var url = response.Headers.Location?.ToString();
        if (string.IsNullOrEmpty(url))
        {
            throw new MicropubException(response.StatusCode, "Upload succeeded but no media URL was returned.");
        }

        return new MediaItem(url);
    }

    /// <summary>
    /// Sends the request and translates connectivity failures (no route to host, DNS failure,
    /// timeout) into <see cref="MicropubException"/> with a null <c>StatusCode</c>, so callers only
    /// ever handle one exception type for Micro.blog failures instead of also having to catch
    /// <see cref="HttpRequestException"/>/<see cref="TaskCanceledException"/> separately. A
    /// <see cref="TaskCanceledException"/> caused by the caller's own
    /// <paramref name="cancellationToken"/> is left alone: that is a deliberate cancellation, not a
    /// failure to report.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new MicropubException(null, ex.Message, ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new MicropubException(null, "The request timed out.", ex);
        }
    }

    internal static async Task ThrowIfErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var message = "Request failed.";
        try
        {
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var error = await JsonSerializer.DeserializeAsync(stream, TransomJsonContext.Default.ApiErrorResponse, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(error?.Error))
            {
                message = error!.Error!;
            }
        }
        catch (JsonException)
        {
            // Response body wasn't the documented {"error": "..."} shape; keep the generic message.
        }

        throw new MicropubException(response.StatusCode, message);
    }
}