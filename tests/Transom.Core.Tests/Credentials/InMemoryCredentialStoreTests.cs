using Transom.Core.Credentials;

namespace Transom.Core.Tests.Credentials;

public class InMemoryCredentialStoreTests
{
    [Fact]
    public void TryGet_BeforeSave_ReturnsNull()
    {
        var store = new InMemoryCredentialStore();

        Assert.Null(store.TryGet("default"));
    }

    [Fact]
    public void Save_ThenTryGet_RoundTrips()
    {
        var store = new InMemoryCredentialStore();

        store.Save("default", "test-token");

        Assert.Equal("test-token", store.TryGet("default"));
    }

    [Fact]
    public void Remove_ClearsTheToken()
    {
        var store = new InMemoryCredentialStore();
        store.Save("default", "test-token");

        store.Remove("default");

        Assert.Null(store.TryGet("default"));
    }

    [Fact]
    public void Save_OverwritesAnExistingToken()
    {
        var store = new InMemoryCredentialStore();
        store.Save("default", "old-token");

        store.Save("default", "new-token");

        Assert.Equal("new-token", store.TryGet("default"));
    }
}