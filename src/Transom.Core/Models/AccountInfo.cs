using System.Text.Json.Serialization;

namespace Transom.Core.Models;

/// <summary>Response of a successful <c>POST /account/verify</c>.</summary>
public sealed record AccountInfo(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("avatar")] string? Avatar,
    [property: JsonPropertyName("default_site")] string? DefaultSite,
    [property: JsonPropertyName("expires_at")] string? ExpiresAt);