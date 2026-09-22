using System.Net.Http.Headers;
using System.Text.Json;

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

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await ThrowIfErrorAsync(response, cancellationToken).ConfigureAwait(false);

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var config = await JsonSerializer.DeserializeAsync(stream, TransomJsonContext.Default.MicropubConfig, cancellationToken).ConfigureAwait(false);
        return config ?? new MicropubConfig();
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