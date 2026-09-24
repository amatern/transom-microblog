using System.Text.Json.Serialization;

namespace Transom.Core.Models;

/// <summary>The <c>{"error": "..."}</c> shape Micro.blog uses for <c>/account/verify</c> failures.</summary>
public sealed record ApiErrorResponse(
    [property: JsonPropertyName("error")] string? Error);