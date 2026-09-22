using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using Transom.Core.Models;

namespace Transom.Core.MicroBlog;

/// <summary>
/// Verifies a Micro.blog app token via <c>POST /account/verify</c> (<c>SPEC.md</c> §6.1). If that
/// endpoint is unreachable or erroring server-side, falls back to <c>GET /micropub?q=config</c> —
/// which also requires a valid Authorization header — as a lighter-weight validity check. A
/// client-rejected token (4xx) is not retried against config: the token really is invalid, and
/// config would reject it the same way with a less specific error.
/// </summary>
public sealed class AccountClient
{
    private readonly HttpClient _httpClient;
    private readonly MicropubClient _micropubClient;

    public AccountClient(HttpClient httpClient, MicropubClient micropubClient)
    {
        _httpClient = httpClient;
        _micropubClient = micropubClient;
    }

    public async Task<AccountInfo> VerifyAsync(string token, CancellationToken cancellationToken)
    {
        try
        {
            return await VerifyDirectAsync(token, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException || IsServerError(ex))
        {
            var config = await _micropubClient.GetConfigAsync(token, cancellationToken).ConfigureAwait(false);
            var defaultSite = config.Destinations.Count > 0 ? config.Destinations[0].Uid : null;
            return new AccountInfo(token, Name: null, Username: string.Empty, Avatar: null, DefaultSite: defaultSite, ExpiresAt: null);
        }
    }

    private async Task<AccountInfo> VerifyDirectAsync(string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/account/verify")
        {
            Content = new FormUrlEncodedContent([new KeyValuePair<string, string>("token", token)]),
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await MicropubClient.ThrowIfErrorAsync(response, cancellationToken).ConfigureAwait(false);

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var account = await JsonSerializer.DeserializeAsync(stream, TransomJsonContext.Default.AccountInfo, cancellationToken).ConfigureAwait(false);
        return account ?? throw new MicropubException(response.StatusCode, "Verify response was empty.");
    }

    private static bool IsServerError(Exception ex) => ex is MicropubException { StatusCode: var status } && (int)status >= 500;
}