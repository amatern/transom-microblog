using System.Text.Json.Serialization;

using Transom.Core.Models;

namespace Transom.Core.MicroBlog;

[JsonSerializable(typeof(BlogInfo))]
[JsonSerializable(typeof(MicropubConfig))]
[JsonSerializable(typeof(AccountInfo))]
[JsonSerializable(typeof(ApiErrorResponse))]
public sealed partial class TransomJsonContext : JsonSerializerContext
{
}