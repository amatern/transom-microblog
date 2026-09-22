namespace Transom.Core.Credentials;

/// <summary>A non-persistent <see cref="ICredentialStore"/>, used by tests and available as a
/// fallback if no platform-specific store is registered.</summary>
public sealed class InMemoryCredentialStore : ICredentialStore
{
    private readonly Dictionary<string, string> _tokens = [];

    public void Save(string accountId, string token) => _tokens[accountId] = token;

    public string? TryGet(string accountId) => _tokens.GetValueOrDefault(accountId);

    public void Remove(string accountId) => _tokens.Remove(accountId);
}