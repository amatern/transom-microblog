using System.Text.Json.Serialization;

namespace Transom.Core.Models;

/// <summary>A blog a Micro.blog account can post to (a Micropub "destination"). <see cref="Name"/>
/// is the domain (e.g. <c>example.micro.blog</c>); <see cref="Title"/> is the human-chosen blog
/// title — SPEC.md §6.2 confirmed both against a real account's <c>q=config</c> response, so a
/// blog picker (M5) should prefer <see cref="DisplayName"/> over <see cref="Name"/>.
/// <see cref="IsDefault"/> reflects the raw <c>microblog-default</c> flag as deserialized;
/// <see cref="Transom.Core.Providers.MicroBlog.MicroBlogProvider"/>'s <c>GetBlogsAsync</c> is what
/// falls back to marking the first destination default when the server flags none.</summary>
public sealed record BlogInfo(
    [property: JsonPropertyName("uid")] string Uid,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("microblog-title")] string? Title,
    [property: JsonPropertyName("microblog-default")] bool IsDefault = false)
{
    public string DisplayName => !string.IsNullOrEmpty(Title) ? Title : Name;
}