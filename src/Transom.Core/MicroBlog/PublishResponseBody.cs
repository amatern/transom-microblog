using System.Text.Json.Serialization;

namespace Transom.Core.MicroBlog;

/// <summary>Body of a draft publish response (<c>SPEC.md</c> §6.2); a published post has no body.</summary>
public sealed record PublishResponseBody(
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("preview")] string? Preview);