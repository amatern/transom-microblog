using System.Text.Json.Serialization;

namespace Transom.Core.Models;

/// <summary>Response of <c>GET /micropub?q=config</c>: the media endpoint and the account's blogs.</summary>
public sealed record MicropubConfig(
    [property: JsonPropertyName("media-endpoint")] string? MediaEndpoint,
    [property: JsonPropertyName("destination")] IReadOnlyList<BlogInfo> Destinations)
{
    public MicropubConfig() : this(null, Array.Empty<BlogInfo>())
    {
    }
}