namespace Transom.Core.Models;

/// <summary>Result of a successful media upload (SPEC.md §6.2: <c>202 Accepted</c>, <c>Location</c>
/// header, no body) — the URL Micro.blog will serve the file from.</summary>
public sealed record MediaItem(string Url);