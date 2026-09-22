namespace Transom.Core.Credentials;

/// <summary>Account identifiers used as the <c>ICredentialStore</c> key. Transom v1 supports one
/// Micro.blog account (SPEC.md §8); multiple accounts are out of scope for v1.</summary>
public static class CredentialAccounts
{
    public const string Default = "default";
}