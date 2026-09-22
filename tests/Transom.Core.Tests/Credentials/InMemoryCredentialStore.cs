using Transom.Core.Credentials;

namespace Transom.Core.Tests.Credentials;

internal sealed class InMemoryCredentialStore : ICredentialStore
{
    private readonly Dictionary<string, string> _tokens = [];

    public void Save(string accountId, string token) => _tokens[accountId] = token;

    public string? TryGet(string accountId) => _tokens.GetValueOrDefault(accountId);

    public void Remove(string accountId) => _tokens.Remove(accountId);
}