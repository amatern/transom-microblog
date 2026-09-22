namespace Transom.Core.Credentials;

/// <summary>
/// Stores app tokens. The only real implementation is <c>PasswordVaultCredentialStore</c> in
/// <c>Transom.App</c> (SPEC.md §8); Core never persists a token itself.
/// </summary>
public interface ICredentialStore
{
    void Save(string accountId, string token);

    string? TryGet(string accountId);

    void Remove(string accountId);
}