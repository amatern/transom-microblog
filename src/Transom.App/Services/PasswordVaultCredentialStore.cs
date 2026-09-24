using Transom.Core.Credentials;

using Windows.Security.Credentials;


namespace Transom.App.Services;

/// <summary>Stores app tokens in Windows Credential Manager via PasswordVault (SPEC.md §8,
/// resource "Transom"). Never logs, prints or persists a token anywhere else.</summary>
public sealed class PasswordVaultCredentialStore : ICredentialStore
{
    private const string Resource = "Transom";

    private readonly PasswordVault _vault = new();

    public void Save(string accountId, string token)
    {
        Remove(accountId);
        _vault.Add(new PasswordCredential(Resource, accountId, token));
    }

    public string? TryGet(string accountId)
    {
        try
        {
            var credential = _vault.Retrieve(Resource, accountId);
            credential.RetrievePassword();
            return credential.Password;
        }
        catch (Exception)
        {
            // PasswordVault has no TryRetrieve; it throws when no matching credential exists.
            return null;
        }
    }

    public void Remove(string accountId)
    {
        try
        {
            _vault.Remove(_vault.Retrieve(Resource, accountId));
        }
        catch (Exception)
        {
            // Nothing to remove.
        }
    }
}