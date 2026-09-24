using System.Text.Json.Serialization;

namespace Transom.Core.Models;

/// <summary>Response of <c>GET /micropub?q=config</c>: the media endpoint and the account's blogs.</summary>
public sealed record MicropubConfig
{
    private IReadOnlyList<BlogInfo> _destinations = Array.Empty<BlogInfo>();

    [JsonPropertyName("media-endpoint")]
    public string? MediaEndpoint { get; init; }

    [JsonPropertyName("destination")]
    public IReadOnlyList<BlogInfo> Destinations
    {
        get => _destinations;
        init => _destinations = value ?? Array.Empty<BlogInfo>();
    }
}