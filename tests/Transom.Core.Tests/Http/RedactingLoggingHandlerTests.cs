using System.Net;
using System.Net.Http.Headers;

using Microsoft.Extensions.Logging;

using Transom.Core.Http;

namespace Transom.Core.Tests.Http;

public class RedactingLoggingHandlerTests
{
    [Fact]
    public async Task SendAsync_RedactsAuthorizationHeaderFromLogs()
    {
        var logger = new CapturingLogger();
        var handler = new RedactingLoggingHandler(logger) { InnerHandler = new FakeHandler() };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://micro.blog/micropub");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");

        await invoker.SendAsync(request, CancellationToken.None);

        Assert.Contains(logger.Messages, m => m.Contains("Bearer [REDACTED]"));
        Assert.DoesNotContain(logger.Messages, m => m.Contains("test-token"));
    }

    [Fact]
    public async Task SendAsync_LogsNoneWhenAuthorizationHeaderAbsent()
    {
        var logger = new CapturingLogger();
        var handler = new RedactingLoggingHandler(logger) { InnerHandler = new FakeHandler() };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://micro.blog/posts/timeline");

        await invoker.SendAsync(request, CancellationToken.None);

        Assert.Contains(logger.Messages, m => m.Contains("(none)"));
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class CapturingLogger : ILogger<RedactingLoggingHandler>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}