using System.Text.Json.Serialization;

namespace Transom.Core.Models;

/// <summary>A blog a Micro.blog account can post to (a Micropub "destination").</summary>
public sealed record BlogInfo(
    [property: JsonPropertyName("uid")] string Uid,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("microblog-title")] string? Title);