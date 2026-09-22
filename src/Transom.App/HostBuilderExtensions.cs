using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Transom.Core.Http;

namespace Transom.App;

internal static class HostBuilderExtensions
{
    public const string MicroBlogHttpClientName = "MicroBlog";

    public static IHostBuilder ConfigureTransomServices(this IHostBuilder builder)
    {
        return builder.ConfigureServices(services =>
        {
            services.AddLogging();
            services.AddTransient<RedactingLoggingHandler>();
            services.AddHttpClient(MicroBlogHttpClientName)
                .AddHttpMessageHandler<RedactingLoggingHandler>();
        });
    }
}