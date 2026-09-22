using System.Reflection;

using CommunityToolkit.Mvvm.ComponentModel;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Transom.App.Services;
using Transom.App.ViewModels;
using Transom.Core.Credentials;
using Transom.Core.Http;
using Transom.Core.MicroBlog;
using Transom.Core.Providers;
using Transom.Core.Providers.MicroBlog;

namespace Transom.App;

internal static class HostBuilderExtensions
{
    private static string UserAgent =>
        $"Transom/{Assembly.GetExecutingAssembly().GetName().Version?.ToString(2) ?? "0.1"} (+https://github.com/amatern/transom-microblog)";

    public static IHostBuilder ConfigureTransomServices(this IHostBuilder builder)
    {
        return builder.ConfigureServices(services =>
        {
            services.AddLogging();
            services.AddTransient<RedactingLoggingHandler>();

            services.AddHttpClient<MicropubClient>(client =>
            {
                client.BaseAddress = new Uri("https://micro.blog");
                client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            }).AddHttpMessageHandler<RedactingLoggingHandler>();

            services.AddHttpClient<AccountClient>(client =>
            {
                client.BaseAddress = new Uri("https://micro.blog");
                client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            }).AddHttpMessageHandler<RedactingLoggingHandler>();

            services.AddSingleton<ICredentialStore, PasswordVaultCredentialStore>();
            services.AddSingleton<IComposerSettings, LocalSettingsComposerSettings>();
            services.AddTransient<IBlogProvider>(sp => new MicroBlogProvider(
                sp.GetRequiredService<MicropubClient>(),
                sp.GetRequiredService<ICredentialStore>(),
                CredentialAccounts.Default));

            services.AddTransient<ComposerViewModel>();
            services.AddTransient<SettingsViewModel>();
        });
    }
}