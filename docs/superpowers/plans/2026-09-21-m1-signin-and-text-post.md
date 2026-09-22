# M1 — Sign in and Text Post Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the user paste a Micro.blog app token, verify it, publish a short text post (with an
auto-appearing Title over 300 characters), and see success/failure feedback that never loses their
text — matching `docs/milestones.md` M1 (stories U1, U2).

**Architecture:** `Transom.Core` gains `MicropubClient` (config + publish) and `AccountClient`
(token verification with a `q=config` fallback) over the "MicroBlog" HTTP pipeline from M0
(`RedactingLoggingHandler`), plus `ICredentialStore`/`InMemoryCredentialStore`, and a trimmed
`IBlogProvider`/`MicroBlogProvider` that reads the current token from the credential store per
call. `Transom.App` gets a `PasswordVault`-backed `ICredentialStore`, `ComposerViewModel` /
`SettingsViewModel` (CommunityToolkit.Mvvm, zero WinUI types), and thin XAML pages wiring them into
the M0 `NavigationView` shell. A new `tests/Transom.App.Tests` project (Windows-only, xUnit) unit
tests the view models per CLAUDE.md Rule 5.

**Tech Stack:** .NET 10, `System.Text.Json` source generation (`TransomJsonContext`),
`Microsoft.Extensions.Http`, CommunityToolkit.Mvvm, xUnit + fake `HttpMessageHandler`,
`Windows.Security.Credentials.PasswordVault`.

**Spec:** `SPEC.md` §3 (U1–U2), §4.2 (Composer), §5 (architecture), §6.1–6.2 (Micro.blog auth and
posting APIs), §8 (storage/security). `docs/milestones.md` M1. Tracking issue: **#2**.

## Global Constraints

- Never log, print, persist or commit tokens; the returned token from `/account/verify` is the one
  stored, never the pasted one blindly (CLAUDE.md Rule 1; SPEC §6.1).
- No real network calls in tests; fixtures live in `tests/Transom.Core.Tests/Fixtures/`
  (CLAUDE.md Rule 2).
- Provider-specific code stays in `Core/Providers/<Name>/`; UI and view models talk only to
  `IBlogProvider`/`ICredentialStore` (CLAUDE.md Rule 3).
- Every async method takes a `CancellationToken`; no `.Result`/`.Wait()` (CLAUDE.md Rule 4).
- View models have no WinUI types and are unit-testable; `[ObservableProperty]`/`[RelayCommand]`
  (CLAUDE.md Rule 5).
- The composer must never lose user text: text survives a failed publish (CLAUDE.md Rule 6).
- Every interactive control gets `AutomationProperties.Name` (CLAUDE.md Rule 7).
- Nullable on; warnings-as-errors in `Transom.Core` (CLAUDE.md Rule 8).
- File-scoped namespaces; `var` when the type is obvious; XML doc comments on public Core APIs
  (CLAUDE.md Style).
- One commit per task, referencing **#2** (not #1 — that's the retroactive M0 issue).

## Discovery notes (context for the executor, not tasks)

- **`/account/verify` is confirmed** (2026-09-21, against
  [help.micro.blog/t/verifying-tokens/102](https://help.micro.blog/t/verifying-tokens/102)) and
  recorded in `SPEC.md` §6.1: `POST /account/verify` (form `token=<token>`) →
  `{token, name, username, avatar, default_site, expires_at}` on success, `{"error": "..."}` on
  failure. Neither doc page shows the HTTP status code used for the error case, so
  `AccountClient` treats any non-2xx as a failure. The returned `token` can differ from what was
  sent — Task 3 stores the *returned* token.
- **`q=config`'s `destination[]` shape when there's exactly one blog is unconfirmed.** The public
  docs show `{"media-endpoint": "..."}` alone (no `destination`) in the minimal example, and a
  separate multi-blog example with `destination: [{uid, name, microblog-title}, ...]`, but no
  combined single-blog example. Task 1's `config-one-blog.json` fixture assumes a one-entry
  `destination` array (matching the multi-blog shape) — **when you test against your real account
  in Task 12's manual smoke test, confirm this and adjust the model/fixture if `destination` is
  absent for a single-blog account.** `MicroBlogProvider.GetBlogsAsync` doesn't special-case an
  empty array (it just returns whatever `Destinations` the config has), so no code change should
  be needed even if the assumption is wrong — only the fixture.
- **401/500 error body shapes are unconfirmed** (the docs don't show Micropub error responses).
  Task 2's `error-401.json`/`error-500.json` fixtures use the same `{"error": "..."}` shape
  `/account/verify` documents, since that's the only confirmed Micro.blog error convention. If
  real responses differ, `MicropubClient`'s error path already degrades gracefully (falls back to
  a generic "Request failed." message on a JSON parse failure), so this is a safe assumption.
- **`IBlogProvider` is intentionally trimmed for M1**, not the full 7-method interface in SPEC
  §5.2. M1 only needs `GetBlogsAsync` and `PublishAsync` on the interface — both read the current
  token from `ICredentialStore` per call, so no method needs a token parameter. The *first*
  verification (pasting a brand-new, not-yet-stored token in Settings) can't go through
  `IBlogProvider` anyway, since there's nothing in the credential store yet to configure a
  provider instance with — so it calls `AccountClient` directly. `VerifyAsync` (the SPEC version,
  no-arg, for periodic re-validation "on app launch or every few days" per Micro.blog's own
  guidance), `GetCategoriesAsync`, `UploadMediaAsync`, `GetRemoteDraftsAsync`, and
  `Capabilities`/`Limits` are deferred to the milestones that use them (M2 media, M5
  categories/drafts/multi-blog, and periodic re-verification isn't in any milestone's checklist
  yet — flag this gap to the user). This mirrors M0's decision to defer `TransomJsonContext` until
  there were real DTOs: build the interface member when a caller exists, not before.
  **`SPEC.md` §5.2 is left as-is** (it documents the v1.0 target shape); this is a phased
  build-out, not a spec change.
- **A new `tests/Transom.App.Tests` project is required.** CLAUDE.md Rule 5 requires view models
  to be unit-testable, but `docs/milestones.md`/CLAUDE.md's only test project is
  `tests/Transom.Core.Tests`, and view models live in `Transom.App` per SPEC §5's layout (not
  Core — Core has zero WinUI/CommunityToolkit.Mvvm dependency by design). Task 9 scaffolds
  `tests/Transom.App.Tests` (same `net10.0-windows10.0.26100.0` TFM as `Transom.App`, since a
  project referencing it can't target plain `net10.0`), adds it to `Transom.sln`, and wires
  `dotnet test tests/Transom.App.Tests --no-restore` into the `build-windows` CI job only (it
  can't build on `ubuntu-latest`, same reason `Transom.App` itself can't).
- **M0's `MicroBlogHttpClientName` named-client registration is replaced with two typed clients**
  (`AddHttpClient<MicropubClient>()`, `AddHttpClient<AccountClient>()`) in Task 11, once there are
  real consumers to configure (base address, `User-Agent`, the M0 redacting handler). This is a
  small edit to M0's `HostBuilderExtensions.cs`, not a new pattern.
- **PasswordVault and `ApplicationData.Current.LocalSettings` cannot be exercised by xUnit** run
  from a plain test host (no package identity) — Tasks 8 and 10's App-layer stores are not unit
  tested; each task's steps include the manual verification to run instead, and the logic that
  *can* be isolated (fallback/default behavior) is pushed into a plain C# class tested via a
  fake, per usual.

---

## Task 1: Config/verify models and `TransomJsonContext`

**Files:**
- Create: `src/Transom.Core/Models/BlogInfo.cs`
- Create: `src/Transom.Core/Models/MicropubConfig.cs`
- Create: `src/Transom.Core/Models/AccountInfo.cs`
- Create: `src/Transom.Core/Models/ApiErrorResponse.cs`
- Create: `src/Transom.Core/MicroBlog/TransomJsonContext.cs`
- Create: `tests/Transom.Core.Tests/Fixtures/FixtureFile.cs`
- Create: `tests/Transom.Core.Tests/Fixtures/config-one-blog.json`
- Create: `tests/Transom.Core.Tests/Fixtures/config-two-blogs.json`
- Create: `tests/Transom.Core.Tests/Fixtures/verify-success.json`
- Create: `tests/Transom.Core.Tests/Fixtures/verify-error.json`
- Create: `tests/Transom.Core.Tests/MicroBlog/TransomJsonContextTests.cs`
- Modify: `tests/Transom.Core.Tests/Transom.Core.Tests.csproj` (copy fixtures to output)

**Interfaces:**
- Consumes: nothing beyond M0's `Transom.Core`/`Transom.Core.Tests` projects.
- Produces: `Transom.Core.Models.{BlogInfo, MicropubConfig, AccountInfo, ApiErrorResponse}` and
  `Transom.Core.MicroBlog.TransomJsonContext` (a `[JsonSerializable]`-registered
  `JsonSerializerContext`). Task 2 adds `MicropubException`/`PublishResponseBody` to the same
  context; Tasks 3–5 consume all four model types.

- [ ] **Step 1: Write the fixture files**

Write `tests/Transom.Core.Tests/Fixtures/config-one-blog.json`:

```json
{
  "media-endpoint": "https://micro.blog/micropub/media",
  "destination": [
    {
      "uid": "https://example.micro.blog/",
      "name": "example.micro.blog",
      "microblog-title": "Example Blog"
    }
  ]
}
```

Write `tests/Transom.Core.Tests/Fixtures/config-two-blogs.json`:

```json
{
  "media-endpoint": "https://micro.blog/micropub/media",
  "destination": [
    {
      "uid": "https://myblog.micro.blog/",
      "name": "myblog.com",
      "microblog-title": "My Blog"
    },
    {
      "uid": "https://anotherblog.micro.blog/",
      "name": "anotherblog.com",
      "microblog-title": "Another blog"
    }
  ]
}
```

Write `tests/Transom.Core.Tests/Fixtures/verify-success.json`:

```json
{
  "token": "HIJKLMNOP",
  "name": "Test User",
  "username": "testuser",
  "avatar": "https://micro.blog/testuser/avatar.jpg",
  "default_site": "testuser.micro.blog",
  "expires_at": null
}
```

Write `tests/Transom.Core.Tests/Fixtures/verify-error.json`:

```json
{
  "error": "App token was not valid."
}
```

- [ ] **Step 2: Copy fixtures to the test output directory**

Edit `tests/Transom.Core.Tests/Transom.Core.Tests.csproj`, adding a new `<ItemGroup>`:

```xml
<ItemGroup>
  <Content Include="Fixtures\**\*.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

- [ ] **Step 3: Write the fixture loader helper**

Write `tests/Transom.Core.Tests/Fixtures/FixtureFile.cs`:

```csharp
namespace Transom.Core.Tests.Fixtures;

internal static class FixtureFile
{
    public static string ReadText(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        return File.ReadAllText(path);
    }

    public static Stream OpenRead(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        return File.OpenRead(path);
    }
}
```

- [ ] **Step 4: Write the failing serialization tests**

Write `tests/Transom.Core.Tests/MicroBlog/TransomJsonContextTests.cs`:

```csharp
using System.Text.Json;
using Transom.Core.MicroBlog;
using Transom.Core.Models;
using Transom.Core.Tests.Fixtures;

namespace Transom.Core.Tests.MicroBlog;

public class TransomJsonContextTests
{
    [Fact]
    public void MicropubConfig_DeserializesOneBlog()
    {
        using var stream = FixtureFile.OpenRead("config-one-blog.json");

        var config = JsonSerializer.Deserialize(stream, TransomJsonContext.Default.MicropubConfig);

        Assert.NotNull(config);
        Assert.Equal("https://micro.blog/micropub/media", config!.MediaEndpoint);
        var blog = Assert.Single(config.Destinations);
        Assert.Equal("https://example.micro.blog/", blog.Uid);
        Assert.Equal("example.micro.blog", blog.Name);
        Assert.Equal("Example Blog", blog.Title);
    }

    [Fact]
    public void MicropubConfig_DeserializesTwoBlogs()
    {
        using var stream = FixtureFile.OpenRead("config-two-blogs.json");

        var config = JsonSerializer.Deserialize(stream, TransomJsonContext.Default.MicropubConfig);

        Assert.NotNull(config);
        Assert.Equal(2, config!.Destinations.Count);
        Assert.Equal("https://myblog.micro.blog/", config.Destinations[0].Uid);
        Assert.Equal("https://anotherblog.micro.blog/", config.Destinations[1].Uid);
    }

    [Fact]
    public void AccountInfo_DeserializesVerifySuccess()
    {
        using var stream = FixtureFile.OpenRead("verify-success.json");

        var account = JsonSerializer.Deserialize(stream, TransomJsonContext.Default.AccountInfo);

        Assert.NotNull(account);
        Assert.Equal("HIJKLMNOP", account!.Token);
        Assert.Equal("testuser", account.Username);
        Assert.Equal("testuser.micro.blog", account.DefaultSite);
    }

    [Fact]
    public void ApiErrorResponse_DeserializesVerifyError()
    {
        using var stream = FixtureFile.OpenRead("verify-error.json");

        var error = JsonSerializer.Deserialize(stream, TransomJsonContext.Default.ApiErrorResponse);

        Assert.NotNull(error);
        Assert.Equal("App token was not valid.", error!.Error);
    }
}
```

- [ ] **Step 5: Run to verify it fails**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test tests\Transom.Core.Tests --filter TransomJsonContextTests
```

Expected: FAIL — `Transom.Core.Models`/`Transom.Core.MicroBlog` types don't exist yet (CS0246).

- [ ] **Step 6: Write the models**

Write `src/Transom.Core/Models/BlogInfo.cs`:

```csharp
using System.Text.Json.Serialization;

namespace Transom.Core.Models;

/// <summary>A blog a Micro.blog account can post to (a Micropub "destination").</summary>
public sealed record BlogInfo(
    [property: JsonPropertyName("uid")] string Uid,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("microblog-title")] string? Title);
```

Write `src/Transom.Core/Models/MicropubConfig.cs`:

```csharp
using System.Text.Json.Serialization;

namespace Transom.Core.Models;

/// <summary>Response of <c>GET /micropub?q=config</c>: the media endpoint and the account's blogs.</summary>
public sealed record MicropubConfig(
    [property: JsonPropertyName("media-endpoint")] string? MediaEndpoint,
    [property: JsonPropertyName("destination")] IReadOnlyList<BlogInfo> Destinations)
{
    public MicropubConfig() : this(null, Array.Empty<BlogInfo>())
    {
    }
}
```

Write `src/Transom.Core/Models/AccountInfo.cs`:

```csharp
using System.Text.Json.Serialization;

namespace Transom.Core.Models;

/// <summary>Response of a successful <c>POST /account/verify</c>.</summary>
public sealed record AccountInfo(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("avatar")] string? Avatar,
    [property: JsonPropertyName("default_site")] string? DefaultSite,
    [property: JsonPropertyName("expires_at")] string? ExpiresAt);
```

Write `src/Transom.Core/Models/ApiErrorResponse.cs`:

```csharp
using System.Text.Json.Serialization;

namespace Transom.Core.Models;

/// <summary>The <c>{"error": "..."}</c> shape Micro.blog uses for <c>/account/verify</c> failures.</summary>
public sealed record ApiErrorResponse(
    [property: JsonPropertyName("error")] string? Error);
```

Note the parameterless `MicropubConfig()` constructor: `System.Text.Json` needs a default value
for `Destinations` when `destination` is absent from the JSON (the "no blogs listed" case this
plan's discovery notes flag as unconfirmed) — without it, a missing `destination` key would
deserialize to `null` instead of an empty list, and `MicroBlogProvider.GetBlogsAsync` (Task 5)
would need a null check it shouldn't need.

- [ ] **Step 7: Write `TransomJsonContext`**

Write `src/Transom.Core/MicroBlog/TransomJsonContext.cs`:

```csharp
using System.Text.Json.Serialization;
using Transom.Core.Models;

namespace Transom.Core.MicroBlog;

[JsonSerializable(typeof(BlogInfo))]
[JsonSerializable(typeof(MicropubConfig))]
[JsonSerializable(typeof(AccountInfo))]
[JsonSerializable(typeof(ApiErrorResponse))]
internal sealed partial class TransomJsonContext : JsonSerializerContext
{
}
```

- [ ] **Step 8: Run to verify it passes**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test tests\Transom.Core.Tests --filter TransomJsonContextTests
```

Expected: `Passed! ... Total: 4`.

- [ ] **Step 9: Full build + format check**

```powershell
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
& $dotnet build Transom.sln -c Debug -p:Platform=x64
& $dotnet format Transom.sln --verify-no-changes
```

- [ ] **Step 10: Commit**

```bash
git add src/Transom.Core/Models src/Transom.Core/MicroBlog tests/Transom.Core.Tests/Fixtures tests/Transom.Core.Tests/MicroBlog tests/Transom.Core.Tests/Transom.Core.Tests.csproj
git commit -m "$(cat <<'EOF'
feat: add config/verify models and TransomJsonContext (#2)

BlogInfo, MicropubConfig, AccountInfo, ApiErrorResponse plus the
source-generated TransomJsonContext; fixtures hand-written from
help.micro.blog's documented q=config and /account/verify shapes.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: `MicropubClient.GetConfigAsync`

**Files:**
- Create: `src/Transom.Core/MicroBlog/MicropubException.cs`
- Create: `src/Transom.Core/MicroBlog/MicropubClient.cs`
- Create: `tests/Transom.Core.Tests/Http/FixtureHttpMessageHandler.cs`
- Create: `tests/Transom.Core.Tests/Fixtures/error-401.json`
- Create: `tests/Transom.Core.Tests/Fixtures/error-500.json`
- Create: `tests/Transom.Core.Tests/MicroBlog/MicropubClientGetConfigTests.cs`

**Interfaces:**
- Consumes: `Transom.Core.Models.{MicropubConfig, ApiErrorResponse}`,
  `Transom.Core.MicroBlog.TransomJsonContext` (Task 1).
- Produces: `Transom.Core.MicroBlog.MicropubException` (`HttpStatusCode StatusCode`, `Message`
  never contains the request token), `Transom.Core.MicroBlog.MicropubClient` with
  `MicropubClient(HttpClient httpClient)` and
  `Task<MicropubConfig> GetConfigAsync(string token, CancellationToken cancellationToken)`. Task 3
  (`AccountClient`) and Task 4 (`PublishAsync`) extend this same class/file. Task 4 also reuses
  `tests/Transom.Core.Tests/Http/FixtureHttpMessageHandler.cs`.

- [ ] **Step 1: Write the fixture files**

Write `tests/Transom.Core.Tests/Fixtures/error-401.json`:

```json
{
  "error": "App token was not valid."
}
```

Write `tests/Transom.Core.Tests/Fixtures/error-500.json`:

```json
{
  "error": "Something went wrong."
}
```

- [ ] **Step 2: Write the reusable fake `HttpMessageHandler`**

Write `tests/Transom.Core.Tests/Http/FixtureHttpMessageHandler.cs`:

```csharp
namespace Transom.Core.Tests.Http;

/// <summary>
/// A fake <see cref="HttpMessageHandler"/> whose response is produced by a caller-supplied
/// delegate, so each test builds the exact <see cref="HttpResponseMessage"/> (status, headers,
/// body) it needs from a fixture file or an in-line string.
/// </summary>
internal sealed class FixtureHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public HttpRequestMessage? LastRequest { get; private set; }

    public FixtureHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _respond = respond;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(_respond(request));
    }
}
```

- [ ] **Step 3: Write the failing tests**

Write `tests/Transom.Core.Tests/MicroBlog/MicropubClientGetConfigTests.cs`:

```csharp
using System.Net;
using Transom.Core.MicroBlog;
using Transom.Core.Tests.Fixtures;
using Transom.Core.Tests.Http;

namespace Transom.Core.Tests.MicroBlog;

public class MicropubClientGetConfigTests
{
    [Fact]
    public async Task GetConfigAsync_OneBlog_ReturnsConfig()
    {
        var handler = new FixtureHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(FixtureFile.ReadText("config-one-blog.json"), System.Text.Encoding.UTF8, "application/json"),
        });
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });

        var config = await client.GetConfigAsync("test-token", CancellationToken.None);

        Assert.Single(config.Destinations);
        Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization!.Scheme);
        Assert.Equal("test-token", handler.LastRequest.Headers.Authorization!.Parameter);
    }

    [Fact]
    public async Task GetConfigAsync_TwoBlogs_ReturnsConfig()
    {
        var handler = new FixtureHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(FixtureFile.ReadText("config-two-blogs.json"), System.Text.Encoding.UTF8, "application/json"),
        });
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });

        var config = await client.GetConfigAsync("test-token", CancellationToken.None);

        Assert.Equal(2, config.Destinations.Count);
    }

    [Fact]
    public async Task GetConfigAsync_401_ThrowsMicropubException()
    {
        var handler = new FixtureHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(FixtureFile.ReadText("error-401.json"), System.Text.Encoding.UTF8, "application/json"),
        });
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });

        var ex = await Assert.ThrowsAsync<MicropubException>(() => client.GetConfigAsync("test-token", CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.DoesNotContain("test-token", ex.Message);
    }

    [Fact]
    public async Task GetConfigAsync_500_ThrowsMicropubException()
    {
        var handler = new FixtureHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(FixtureFile.ReadText("error-500.json"), System.Text.Encoding.UTF8, "application/json"),
        });
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });

        var ex = await Assert.ThrowsAsync<MicropubException>(() => client.GetConfigAsync("test-token", CancellationToken.None));

        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
    }
}
```

- [ ] **Step 4: Run to verify it fails**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test tests\Transom.Core.Tests --filter MicropubClientGetConfigTests
```

Expected: FAIL — `MicropubClient`/`MicropubException` don't exist (CS0246).

- [ ] **Step 5: Write `MicropubException`**

Write `src/Transom.Core/MicroBlog/MicropubException.cs`:

```csharp
using System.Net;

namespace Transom.Core.MicroBlog;

/// <summary>
/// Thrown when a Micro.blog API call fails. The message never includes the request's
/// Authorization token (CLAUDE.md Rule 1) — it carries only the status code and, when parseable,
/// the response's <c>error</c> field.
/// </summary>
public sealed class MicropubException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public MicropubException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
```

- [ ] **Step 6: Write `MicropubClient`**

Write `src/Transom.Core/MicroBlog/MicropubClient.cs`:

```csharp
using System.Net.Http.Headers;
using System.Text.Json;
using Transom.Core.Models;

namespace Transom.Core.MicroBlog;

/// <summary>Client for Micro.blog's Micropub endpoints (<c>SPEC.md</c> §6.2).</summary>
public sealed class MicropubClient
{
    private readonly HttpClient _httpClient;

    public MicropubClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<MicropubConfig> GetConfigAsync(string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/micropub?q=config");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await ThrowIfErrorAsync(response, cancellationToken).ConfigureAwait(false);

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var config = await JsonSerializer.DeserializeAsync(stream, TransomJsonContext.Default.MicropubConfig, cancellationToken).ConfigureAwait(false);
        return config ?? new MicropubConfig();
    }

    internal static async Task ThrowIfErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var message = "Request failed.";
        try
        {
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var error = await JsonSerializer.DeserializeAsync(stream, TransomJsonContext.Default.ApiErrorResponse, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(error?.Error))
            {
                message = error!.Error!;
            }
        }
        catch (JsonException)
        {
            // Response body wasn't the documented {"error": "..."} shape; keep the generic message.
        }

        throw new MicropubException(response.StatusCode, message);
    }
}
```

- [ ] **Step 7: Run to verify it passes**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test tests\Transom.Core.Tests --filter MicropubClientGetConfigTests
```

Expected: `Passed! ... Total: 4`.

- [ ] **Step 8: Full build + format check**

```powershell
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
& $dotnet build Transom.sln -c Debug -p:Platform=x64
& $dotnet format Transom.sln --verify-no-changes
```

- [ ] **Step 9: Commit**

```bash
git add src/Transom.Core/MicroBlog/MicropubException.cs src/Transom.Core/MicroBlog/MicropubClient.cs tests/Transom.Core.Tests/Http tests/Transom.Core.Tests/Fixtures/error-401.json tests/Transom.Core.Tests/Fixtures/error-500.json tests/Transom.Core.Tests/MicroBlog/MicropubClientGetConfigTests.cs
git commit -m "$(cat <<'EOF'
feat: add MicropubClient.GetConfigAsync (#2)

Bearer-authenticated GET /micropub?q=config; MicropubException carries
the status code and a status-derived message, never the token.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: `AccountClient.VerifyAsync` with `q=config` fallback

**Files:**
- Create: `src/Transom.Core/MicroBlog/AccountClient.cs`
- Create: `tests/Transom.Core.Tests/MicroBlog/AccountClientTests.cs`

**Interfaces:**
- Consumes: `Transom.Core.MicroBlog.{MicropubClient, MicropubException}` (Task 2),
  `Transom.Core.Models.AccountInfo` (Task 1).
- Produces: `Transom.Core.MicroBlog.AccountClient` with
  `AccountClient(HttpClient httpClient, MicropubClient micropubClient)` and
  `Task<AccountInfo> VerifyAsync(string token, CancellationToken cancellationToken)`. Task 10
  (`SettingsViewModel`) is the consumer.

- [ ] **Step 1: Write the failing tests**

Write `tests/Transom.Core.Tests/MicroBlog/AccountClientTests.cs`:

```csharp
using System.Net;
using System.Text;
using Transom.Core.MicroBlog;
using Transom.Core.Tests.Fixtures;
using Transom.Core.Tests.Http;

namespace Transom.Core.Tests.MicroBlog;

public class AccountClientTests
{
    [Fact]
    public async Task VerifyAsync_Success_ReturnsAccountInfo()
    {
        var handler = new FixtureHttpMessageHandler(request => request.RequestUri!.AbsolutePath == "/account/verify"
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(FixtureFile.ReadText("verify-success.json"), Encoding.UTF8, "application/json"),
            }
            : throw new InvalidOperationException("Unexpected request: " + request.RequestUri));
        var client = BuildClient(handler);

        var account = await client.VerifyAsync("pasted-token", CancellationToken.None);

        Assert.Equal("HIJKLMNOP", account.Token);
        Assert.Equal("testuser", account.Username);
    }

    [Fact]
    public async Task VerifyAsync_InvalidToken_ThrowsWithoutFallingBack()
    {
        var configRequested = false;
        var handler = new FixtureHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/account/verify")
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent(FixtureFile.ReadText("verify-error.json"), Encoding.UTF8, "application/json"),
                };
            }

            configRequested = true;
            throw new InvalidOperationException("q=config should not be called for a 401.");
        });
        var client = BuildClient(handler);

        var ex = await Assert.ThrowsAsync<MicropubException>(() => client.VerifyAsync("bad-token", CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.False(configRequested);
    }

    [Fact]
    public async Task VerifyAsync_ServerError_FallsBackToConfig()
    {
        var handler = new FixtureHttpMessageHandler(request => request.RequestUri!.AbsolutePath == "/account/verify"
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(FixtureFile.ReadText("error-500.json"), Encoding.UTF8, "application/json"),
            }
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(FixtureFile.ReadText("config-one-blog.json"), Encoding.UTF8, "application/json"),
            });
        var client = BuildClient(handler);

        var account = await client.VerifyAsync("test-token", CancellationToken.None);

        Assert.Equal("test-token", account.Token);
        Assert.Equal("https://example.micro.blog/", account.DefaultSite);
        Assert.Equal(string.Empty, account.Username);
    }

    private static AccountClient BuildClient(FixtureHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") };
        return new AccountClient(httpClient, new MicropubClient(httpClient));
    }
}
```

- [ ] **Step 2: Run to verify it fails**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test tests\Transom.Core.Tests --filter AccountClientTests
```

Expected: FAIL — `AccountClient` doesn't exist (CS0246).

- [ ] **Step 3: Write `AccountClient`**

Write `src/Transom.Core/MicroBlog/AccountClient.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Transom.Core.Models;

namespace Transom.Core.MicroBlog;

/// <summary>
/// Verifies a Micro.blog app token via <c>POST /account/verify</c> (<c>SPEC.md</c> §6.1). If that
/// endpoint is unreachable or erroring server-side, falls back to <c>GET /micropub?q=config</c> —
/// which also requires a valid Authorization header — as a lighter-weight validity check. A
/// client-rejected token (4xx) is not retried against config: the token really is invalid, and
/// config would reject it the same way with a less specific error.
/// </summary>
public sealed class AccountClient
{
    private readonly HttpClient _httpClient;
    private readonly MicropubClient _micropubClient;

    public AccountClient(HttpClient httpClient, MicropubClient micropubClient)
    {
        _httpClient = httpClient;
        _micropubClient = micropubClient;
    }

    public async Task<AccountInfo> VerifyAsync(string token, CancellationToken cancellationToken)
    {
        try
        {
            return await VerifyDirectAsync(token, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException || IsServerError(ex))
        {
            var config = await _micropubClient.GetConfigAsync(token, cancellationToken).ConfigureAwait(false);
            var defaultSite = config.Destinations.Count > 0 ? config.Destinations[0].Uid : null;
            return new AccountInfo(token, Name: null, Username: string.Empty, Avatar: null, DefaultSite: defaultSite, ExpiresAt: null);
        }
    }

    private async Task<AccountInfo> VerifyDirectAsync(string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/account/verify")
        {
            Content = new FormUrlEncodedContent([new KeyValuePair<string, string>("token", token)]),
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await MicropubClient.ThrowIfErrorAsync(response, cancellationToken).ConfigureAwait(false);

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var account = await JsonSerializer.DeserializeAsync(stream, TransomJsonContext.Default.AccountInfo, cancellationToken).ConfigureAwait(false);
        return account ?? throw new MicropubException(response.StatusCode, "Verify response was empty.");
    }

    private static bool IsServerError(Exception ex) => ex is MicropubException { StatusCode: var status } && (int)status >= 500;
}
```

- [ ] **Step 4: Run to verify it passes**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test tests\Transom.Core.Tests --filter AccountClientTests
```

Expected: `Passed! ... Total: 3`.

- [ ] **Step 5: Full build + format check**

```powershell
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
& $dotnet build Transom.sln -c Debug -p:Platform=x64
& $dotnet format Transom.sln --verify-no-changes
```

- [ ] **Step 6: Commit**

```bash
git add src/Transom.Core/MicroBlog/AccountClient.cs tests/Transom.Core.Tests/MicroBlog/AccountClientTests.cs
git commit -m "$(cat <<'EOF'
feat: add AccountClient.VerifyAsync with q=config fallback (#2)

A 4xx from /account/verify is a real rejection (no fallback); a 5xx
or network failure falls back to q=config as a lighter-weight check,
per SPEC.md §6.1's documented fallback.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: `MicropubClient.PublishAsync`

**Files:**
- Create: `src/Transom.Core/Models/PostDraft.cs`
- Create: `src/Transom.Core/Models/PublishResult.cs`
- Create: `src/Transom.Core/MicroBlog/PublishResponseBody.cs`
- Modify: `src/Transom.Core/MicroBlog/TransomJsonContext.cs` (register `PublishResponseBody`)
- Modify: `src/Transom.Core/MicroBlog/MicropubClient.cs` (add `PublishAsync`)
- Create: `tests/Transom.Core.Tests/Fixtures/publish-draft-response.json`
- Create: `tests/Transom.Core.Tests/MicroBlog/MicropubClientPublishTests.cs`

**Interfaces:**
- Consumes: `Transom.Core.MicroBlog.MicropubClient` (Task 2).
- Produces: `Transom.Core.Models.PostDraft(string Content, string? Title, bool PostAsDraft)`,
  `Transom.Core.Models.PublishResult(string Url, string? PreviewUrl)`,
  `MicropubClient.PublishAsync(string token, PostDraft draft, CancellationToken cancellationToken)
  -> Task<PublishResult>`. Tasks 5 (`MicroBlogProvider`) and 9 (`ComposerViewModel`) consume
  `PostDraft`/`PublishResult`.

- [ ] **Step 1: Write the fixture file**

A normal (non-draft) publish is `201 Created` / `202 Accepted` with a `Location` header and no
body (SPEC §6.2) — that's built directly in the test below, since there's no JSON body to fixture.
A **draft** publish's body is the one JSON shape worth a fixture:

Write `tests/Transom.Core.Tests/Fixtures/publish-draft-response.json`:

```json
{
  "url": "https://example.micro.blog/2026/09/21/hello-world.html",
  "preview": "https://micro.blog/account/posts/123/preview/456"
}
```

- [ ] **Step 2: Write the failing tests**

Write `tests/Transom.Core.Tests/MicroBlog/MicropubClientPublishTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Transom.Core.MicroBlog;
using Transom.Core.Models;
using Transom.Core.Tests.Fixtures;
using Transom.Core.Tests.Http;

namespace Transom.Core.Tests.MicroBlog;

public class MicropubClientPublishTests
{
    [Fact]
    public async Task PublishAsync_Published_ReturnsLocationAsUrl()
    {
        var handler = new FixtureHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Created);
            response.Headers.Location = new Uri("https://example.micro.blog/2026/09/21/hello.html");
            return response;
        });
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });

        var result = await client.PublishAsync("test-token", new PostDraft("Hello, world!", null, PostAsDraft: false), CancellationToken.None);

        Assert.Equal("https://example.micro.blog/2026/09/21/hello.html", result.Url);
        Assert.Null(result.PreviewUrl);
    }

    [Fact]
    public async Task PublishAsync_Draft_ReturnsUrlAndPreviewFromBody()
    {
        var handler = new FixtureHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Accepted)
        {
            Content = new StringContent(FixtureFile.ReadText("publish-draft-response.json"), Encoding.UTF8, "application/json"),
        });
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });

        var result = await client.PublishAsync("test-token", new PostDraft("Draft text", "A title", PostAsDraft: true), CancellationToken.None);

        Assert.Equal("https://example.micro.blog/2026/09/21/hello-world.html", result.Url);
        Assert.Equal("https://micro.blog/account/posts/123/preview/456", result.PreviewUrl);
    }

    [Fact]
    public async Task PublishAsync_SendsExpectedFormFields()
    {
        var handler = new FixtureHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Created);
            response.Headers.Location = new Uri("https://example.micro.blog/p.html");
            return response;
        });
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });

        await client.PublishAsync("test-token", new PostDraft("Body text", "My Title", PostAsDraft: true), CancellationToken.None);

        var form = await handler.LastRequest!.Content!.ReadAsStringAsync();
        Assert.Contains("h=entry", form);
        Assert.Contains("content=Body+text", form);
        Assert.Contains("name=My+Title", form);
        Assert.Contains("post-status=draft", form);
    }

    [Fact]
    public async Task PublishAsync_401_ThrowsWithoutLeakingToken()
    {
        var handler = new FixtureHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(FixtureFile.ReadText("error-401.json"), Encoding.UTF8, "application/json"),
        });
        var client = new MicropubClient(new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") });

        var ex = await Assert.ThrowsAsync<MicropubException>(() => client.PublishAsync("test-token", new PostDraft("x", null, false), CancellationToken.None));

        Assert.DoesNotContain("test-token", ex.Message);
    }
}
```

- [ ] **Step 3: Run to verify it fails**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test tests\Transom.Core.Tests --filter MicropubClientPublishTests
```

Expected: FAIL — `PostDraft`/`PublishResult`/`PublishAsync` don't exist (CS0246/CS1061).

- [ ] **Step 4: Write the models**

Write `src/Transom.Core/Models/PostDraft.cs`:

```csharp
namespace Transom.Core.Models;

/// <summary>A post ready to publish. <paramref name="PostAsDraft"/> maps to <c>post-status=draft</c> (SPEC §6.2).</summary>
public sealed record PostDraft(string Content, string? Title, bool PostAsDraft);
```

Write `src/Transom.Core/Models/PublishResult.cs`:

```csharp
namespace Transom.Core.Models;

/// <summary>Result of a successful publish: the post's URL, and a preview URL for drafts.</summary>
public sealed record PublishResult(string Url, string? PreviewUrl);
```

- [ ] **Step 5: Write the internal response-body DTO and register it**

Write `src/Transom.Core/MicroBlog/PublishResponseBody.cs`:

```csharp
using System.Text.Json.Serialization;

namespace Transom.Core.MicroBlog;

/// <summary>Body of a draft publish response (<c>SPEC.md</c> §6.2); a published post has no body.</summary>
internal sealed record PublishResponseBody(
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("preview")] string? Preview);
```

Edit `src/Transom.Core/MicroBlog/TransomJsonContext.cs`, adding the attribute:

```csharp
[JsonSerializable(typeof(BlogInfo))]
[JsonSerializable(typeof(MicropubConfig))]
[JsonSerializable(typeof(AccountInfo))]
[JsonSerializable(typeof(ApiErrorResponse))]
[JsonSerializable(typeof(PublishResponseBody))]
internal sealed partial class TransomJsonContext : JsonSerializerContext
{
}
```

- [ ] **Step 6: Add `PublishAsync` to `MicropubClient`**

Edit `src/Transom.Core/MicroBlog/MicropubClient.cs`, adding this method to the class (after
`GetConfigAsync`):

```csharp
    public async Task<PublishResult> PublishAsync(string token, PostDraft draft, CancellationToken cancellationToken)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new("h", "entry"),
            new("content", draft.Content),
        };
        if (!string.IsNullOrEmpty(draft.Title))
        {
            form.Add(new("name", draft.Title));
        }
        if (draft.PostAsDraft)
        {
            form.Add(new("post-status", "draft"));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/micropub")
        {
            Content = new FormUrlEncodedContent(form),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await ThrowIfErrorAsync(response, cancellationToken).ConfigureAwait(false);

        if (response.Headers.Location is { } location)
        {
            return new PublishResult(location.ToString(), null);
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var body = await JsonSerializer.DeserializeAsync(stream, TransomJsonContext.Default.PublishResponseBody, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(body?.Url))
        {
            throw new MicropubException(response.StatusCode, "Publish succeeded but no post URL was returned.");
        }

        return new PublishResult(body.Url, body.Preview);
    }
```

Add `using Transom.Core.Models;` to the top of the file if not already present (it is, from Task 2).

- [ ] **Step 7: Run to verify it passes**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test tests\Transom.Core.Tests --filter MicropubClientPublishTests
```

Expected: `Passed! ... Total: 4`.

- [ ] **Step 8: Full build + format check**

```powershell
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
& $dotnet build Transom.sln -c Debug -p:Platform=x64
& $dotnet format Transom.sln --verify-no-changes
```

- [ ] **Step 9: Commit**

```bash
git add src/Transom.Core/Models/PostDraft.cs src/Transom.Core/Models/PublishResult.cs src/Transom.Core/MicroBlog/PublishResponseBody.cs src/Transom.Core/MicroBlog/TransomJsonContext.cs src/Transom.Core/MicroBlog/MicropubClient.cs tests/Transom.Core.Tests/Fixtures/publish-draft-response.json tests/Transom.Core.Tests/MicroBlog/MicropubClientPublishTests.cs
git commit -m "$(cat <<'EOF'
feat: add MicropubClient.PublishAsync (#2)

Form-encodes h=entry/content/name/post-status; reads the post URL
from the Location header when present, else from a draft's JSON
body (url + preview), per SPEC.md §6.2.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: `IBlogProvider` / `MicroBlogProvider`

**Files:**
- Create: `src/Transom.Core/Credentials/ICredentialStore.cs`
- Create: `src/Transom.Core/Providers/IBlogProvider.cs`
- Create: `src/Transom.Core/Providers/MicroBlog/MicroBlogProvider.cs`
- Create: `tests/Transom.Core.Tests/Credentials/InMemoryCredentialStore.cs` *(test-only fake; see Task 6 note)*
- Create: `tests/Transom.Core.Tests/Providers/MicroBlogProviderTests.cs`

**Interfaces:**
- Consumes: `Transom.Core.MicroBlog.MicropubClient` (Task 2/4), `Transom.Core.Models.{BlogInfo,
  PostDraft, PublishResult}` (Tasks 1/4).
- Produces: `Transom.Core.Credentials.ICredentialStore` (`void Save(string accountId, string
  token)`, `string? TryGet(string accountId)`, `void Remove(string accountId)`),
  `Transom.Core.Providers.IBlogProvider` (`string Id { get; }`,
  `Task<IReadOnlyList<BlogInfo>> GetBlogsAsync(CancellationToken)`,
  `Task<PublishResult> PublishAsync(PostDraft, CancellationToken)`),
  `Transom.Core.Providers.MicroBlog.MicroBlogProvider : IBlogProvider` with constructor
  `MicroBlogProvider(MicropubClient micropubClient, ICredentialStore credentialStore, string
  accountId)`. Task 6 builds the real (non-test) `InMemoryCredentialStore` this task's test file
  temporarily duplicates — Task 6 deletes the duplicate and points the test at the shared one (see
  Task 6 Step 1).

- [ ] **Step 1: Write `ICredentialStore`**

Write `src/Transom.Core/Credentials/ICredentialStore.cs`:

```csharp
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
```

- [ ] **Step 2: Write a minimal in-test fake so this task's tests don't block on Task 6**

Write `tests/Transom.Core.Tests/Credentials/InMemoryCredentialStore.cs`:

```csharp
using Transom.Core.Credentials;

namespace Transom.Core.Tests.Credentials;

internal sealed class InMemoryCredentialStore : ICredentialStore
{
    private readonly Dictionary<string, string> _tokens = [];

    public void Save(string accountId, string token) => _tokens[accountId] = token;

    public string? TryGet(string accountId) => _tokens.GetValueOrDefault(accountId);

    public void Remove(string accountId) => _tokens.Remove(accountId);
}
```

*(Task 6 promotes this exact class to `src/Transom.Core/Credentials/InMemoryCredentialStore.cs` as
a real, public, reusable fake — see Task 6 Step 1, which deletes this file and repoints the
`using`.)*

- [ ] **Step 3: Write the failing tests**

Write `tests/Transom.Core.Tests/Providers/MicroBlogProviderTests.cs`:

```csharp
using System.Net;
using System.Text;
using Transom.Core.MicroBlog;
using Transom.Core.Models;
using Transom.Core.Providers.MicroBlog;
using Transom.Core.Tests.Credentials;
using Transom.Core.Tests.Fixtures;
using Transom.Core.Tests.Http;

namespace Transom.Core.Tests.Providers;

public class MicroBlogProviderTests
{
    private const string AccountId = "default";

    [Fact]
    public async Task GetBlogsAsync_ReturnsDestinationsFromConfig()
    {
        var handler = new FixtureHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(FixtureFile.ReadText("config-two-blogs.json"), Encoding.UTF8, "application/json"),
        });
        var provider = BuildProvider(handler, "test-token");

        var blogs = await provider.GetBlogsAsync(CancellationToken.None);

        Assert.Equal(2, blogs.Count);
    }

    [Fact]
    public async Task PublishAsync_NoStoredToken_Throws()
    {
        var handler = new FixtureHttpMessageHandler(_ => throw new InvalidOperationException("Should not call the network without a token."));
        var provider = BuildProvider(handler, token: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.PublishAsync(new PostDraft("x", null, false), CancellationToken.None));
    }

    [Fact]
    public async Task PublishAsync_Failure_DoesNotLeakTokenInExceptionMessage()
    {
        var handler = new FixtureHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(FixtureFile.ReadText("error-401.json"), Encoding.UTF8, "application/json"),
        });
        var provider = BuildProvider(handler, "super-secret-token");

        var ex = await Assert.ThrowsAsync<MicropubException>(() => provider.PublishAsync(new PostDraft("x", null, false), CancellationToken.None));

        Assert.DoesNotContain("super-secret-token", ex.Message);
        Assert.DoesNotContain("super-secret-token", ex.ToString());
    }

    [Fact]
    public void Id_IsMicroblog()
    {
        var provider = BuildProvider(new FixtureHttpMessageHandler(_ => throw new InvalidOperationException()), "test-token");

        Assert.Equal("microblog", provider.Id);
    }

    private static MicroBlogProvider BuildProvider(FixtureHttpMessageHandler handler, string? token)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") };
        var credentialStore = new InMemoryCredentialStore();
        if (token is not null)
        {
            credentialStore.Save(AccountId, token);
        }

        return new MicroBlogProvider(new MicropubClient(httpClient), credentialStore, AccountId);
    }
}
```

- [ ] **Step 4: Run to verify it fails**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test tests\Transom.Core.Tests --filter MicroBlogProviderTests
```

Expected: FAIL — `IBlogProvider`/`MicroBlogProvider` don't exist (CS0246).

- [ ] **Step 5: Write `IBlogProvider`**

Write `src/Transom.Core/Providers/IBlogProvider.cs`:

```csharp
using Transom.Core.Models;

namespace Transom.Core.Providers;

/// <summary>
/// A blogging platform Transom can publish to. This is a subset of SPEC.md §5.2's target
/// interface — <c>VerifyAsync</c>, <c>GetCategoriesAsync</c>, <c>UploadMediaAsync</c>,
/// <c>GetRemoteDraftsAsync</c>, <c>Capabilities</c> and <c>Limits</c> are added by the milestones
/// that need them (M2 media, M5 categories/drafts/multi-blog UI); M1 only needs to list blogs and
/// publish. Implementations read the current token themselves (e.g. from
/// <see cref="Transom.Core.Credentials.ICredentialStore"/>) — no method here takes one, so nothing
/// outside <c>Providers/&lt;name&gt;/</c> ever handles a raw token (CLAUDE.md Rule 3).
/// </summary>
public interface IBlogProvider
{
    string Id { get; }

    Task<IReadOnlyList<BlogInfo>> GetBlogsAsync(CancellationToken cancellationToken);

    Task<PublishResult> PublishAsync(PostDraft draft, CancellationToken cancellationToken);
}
```

- [ ] **Step 6: Write `MicroBlogProvider`**

Write `src/Transom.Core/Providers/MicroBlog/MicroBlogProvider.cs`:

```csharp
using Transom.Core.Credentials;
using Transom.Core.MicroBlog;
using Transom.Core.Models;

namespace Transom.Core.Providers.MicroBlog;

/// <summary>The Micro.blog <see cref="IBlogProvider"/>. Provider-specific — nothing outside this
/// folder may reference it directly; callers use <see cref="IBlogProvider"/> (CLAUDE.md Rule 3).</summary>
public sealed class MicroBlogProvider : IBlogProvider
{
    private readonly MicropubClient _micropubClient;
    private readonly ICredentialStore _credentialStore;
    private readonly string _accountId;

    public string Id => "microblog";

    public MicroBlogProvider(MicropubClient micropubClient, ICredentialStore credentialStore, string accountId)
    {
        _micropubClient = micropubClient;
        _credentialStore = credentialStore;
        _accountId = accountId;
    }

    public async Task<IReadOnlyList<BlogInfo>> GetBlogsAsync(CancellationToken cancellationToken)
    {
        var config = await _micropubClient.GetConfigAsync(RequireToken(), cancellationToken).ConfigureAwait(false);
        return config.Destinations;
    }

    public Task<PublishResult> PublishAsync(PostDraft draft, CancellationToken cancellationToken)
    {
        return _micropubClient.PublishAsync(RequireToken(), draft, cancellationToken);
    }

    private string RequireToken()
    {
        return _credentialStore.TryGet(_accountId)
            ?? throw new InvalidOperationException("No Micro.blog account is signed in.");
    }
}
```

- [ ] **Step 7: Run to verify it passes**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test tests\Transom.Core.Tests --filter MicroBlogProviderTests
```

Expected: `Passed! ... Total: 4`.

- [ ] **Step 8: Full build + format check**

```powershell
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
& $dotnet build Transom.sln -c Debug -p:Platform=x64
& $dotnet format Transom.sln --verify-no-changes
```

- [ ] **Step 9: Commit**

```bash
git add src/Transom.Core/Credentials/ICredentialStore.cs src/Transom.Core/Providers tests/Transom.Core.Tests/Credentials tests/Transom.Core.Tests/Providers
git commit -m "$(cat <<'EOF'
feat: add IBlogProvider and MicroBlogProvider (#2)

Trimmed to what M1 needs (GetBlogsAsync, PublishAsync); each call
reads the current token from ICredentialStore, so no method takes
one. Verified PublishAsync's failure exception never contains the
token, in message or ToString().

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: `InMemoryCredentialStore` (Core, real)

**Files:**
- Create: `src/Transom.Core/Credentials/InMemoryCredentialStore.cs`
- Delete: `tests/Transom.Core.Tests/Credentials/InMemoryCredentialStore.cs` (Task 5's temporary fake)
- Modify: `tests/Transom.Core.Tests/Providers/MicroBlogProviderTests.cs` (repoint the `using`)
- Create: `tests/Transom.Core.Tests/Credentials/InMemoryCredentialStoreTests.cs`

**Interfaces:**
- Consumes: `Transom.Core.Credentials.ICredentialStore` (Task 5).
- Produces: `Transom.Core.Credentials.InMemoryCredentialStore : ICredentialStore` (public, in
  Core — usable both as a test double for any project and as a documented non-persistent
  fallback). No later task consumes anything new here beyond the type itself.

- [ ] **Step 1: Promote the fake and repoint its one usage**

```bash
git mv tests/Transom.Core.Tests/Credentials/InMemoryCredentialStore.cs src/Transom.Core/Credentials/InMemoryCredentialStore.cs
```

Edit the moved `src/Transom.Core/Credentials/InMemoryCredentialStore.cs` to make it public and
match its new home:

```csharp
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
```

Edit `tests/Transom.Core.Tests/Providers/MicroBlogProviderTests.cs`, replacing
`using Transom.Core.Tests.Credentials;` with `using Transom.Core.Credentials;` (the type is now
`Transom.Core.Credentials.InMemoryCredentialStore`, no longer `Transom.Core.Tests.Credentials`).

- [ ] **Step 2: Run the provider tests to confirm the repoint didn't break anything**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test tests\Transom.Core.Tests --filter MicroBlogProviderTests
```

Expected: `Passed! ... Total: 4`.

- [ ] **Step 3: Write the failing tests for the store itself**

Write `tests/Transom.Core.Tests/Credentials/InMemoryCredentialStoreTests.cs`:

```csharp
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
```

(This step comes *after* writing the implementation because Step 1 already promoted a working
implementation — these tests exercise the promoted class, not drive a new one. They should pass
immediately; that's expected here, not a TDD violation, since the behavior was already built and
verified indirectly by Task 5's provider tests.)

- [ ] **Step 4: Run to verify it passes**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test tests\Transom.Core.Tests --filter InMemoryCredentialStoreTests
```

Expected: `Passed! ... Total: 4`.

- [ ] **Step 5: Full build + format check**

```powershell
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
& $dotnet build Transom.sln -c Debug -p:Platform=x64
& $dotnet format Transom.sln --verify-no-changes
```

- [ ] **Step 6: Commit**

```bash
git add src/Transom.Core/Credentials/InMemoryCredentialStore.cs tests/Transom.Core.Tests/Credentials tests/Transom.Core.Tests/Providers/MicroBlogProviderTests.cs
git commit -m "$(cat <<'EOF'
test: promote InMemoryCredentialStore to a public Core fake (#2)

Task 5 needed it early to avoid blocking on this task; now it's a
real, directly-tested public type other tests and (if ever needed)
non-Windows front-ends can reuse.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 7: `PasswordVaultCredentialStore` (App)

**Files:**
- Create: `src/Transom.App/Services/PasswordVaultCredentialStore.cs`

**Interfaces:**
- Consumes: `Transom.Core.Credentials.ICredentialStore` (Task 5).
- Produces: `Transom.App.Services.PasswordVaultCredentialStore : ICredentialStore` (no
  constructor parameters). Task 11 registers it in DI.

No automated test: `Windows.Security.Credentials.PasswordVault` needs package identity, which a
plain xUnit host (even in `Transom.App.Tests`, added in Task 9) doesn't have — it throws
`Exception (HRESULT: 0x80070490 or similar)` outside a packaged/activated app. This is why
`ICredentialStore` exists as a seam at all: `Transom.Core`'s tests (Task 5, Task 6) exercise all
the *logic* that depends on a credential store, against `InMemoryCredentialStore`. This task is
manually verified in Task 12's smoke test instead.

- [ ] **Step 1: Write `PasswordVaultCredentialStore`**

Write `src/Transom.App/Services/PasswordVaultCredentialStore.cs`:

```csharp
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
```

- [ ] **Step 2: Build**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" build Transom.sln -c Debug -p:Platform=x64
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 3: Format check**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" format Transom.sln --verify-no-changes
```

- [ ] **Step 4: Commit**

```bash
git add src/Transom.App/Services/PasswordVaultCredentialStore.cs
git commit -m "$(cat <<'EOF'
feat: add PasswordVaultCredentialStore (#2)

Windows.Security.Credentials.PasswordVault-backed ICredentialStore,
resource "Transom" per SPEC.md §8. Not unit tested: PasswordVault
needs package identity, unavailable to a plain xUnit host; verified
manually in Task 12.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 8: Debug-only draft-safety setting

**Files:**
- Create: `src/Transom.App/Services/IComposerSettings.cs`
- Create: `src/Transom.App/Services/LocalSettingsComposerSettings.cs`
- Modify: `SPEC.md` §4.4 (document the new Settings toggle)

**Interfaces:**
- Consumes: nothing new.
- Produces: `Transom.App.Services.IComposerSettings` (`bool PostAsDraft { get; set; }`),
  `Transom.App.Services.LocalSettingsComposerSettings : IComposerSettings`. Task 9
  (`ComposerViewModel`) consumes `IComposerSettings` (via a fake in tests); Task 11 registers the
  real implementation in DI and adds a Settings-page toggle bound to it.

Same testability limit as Task 7: `ApplicationData.Current.LocalSettings` needs package identity.
`LocalSettingsComposerSettings` is a thin wrapper with no branchy logic beyond the `#if DEBUG`
default (there's nothing worth extracting into a testable pure function), so it's covered by
Task 12's manual smoke test instead. `ComposerViewModel`'s Task 9 tests exercise the
`PostAsDraft`-dependent behavior against a plain fake.

- [ ] **Step 1: Write `IComposerSettings`**

Write `src/Transom.App/Services/IComposerSettings.cs`:

```csharp
namespace Transom.App.Services;

/// <summary>Local, per-device composer preferences (SPEC.md §4.4).</summary>
public interface IComposerSettings
{
    /// <summary>
    /// When true, publishes go out with <c>post-status=draft</c> instead of live, so manual
    /// testing against a real account doesn't create public posts. Defaults to <c>true</c> in
    /// Debug builds, <c>false</c> in Release.
    /// </summary>
    bool PostAsDraft { get; set; }
}
```

- [ ] **Step 2: Write `LocalSettingsComposerSettings`**

Write `src/Transom.App/Services/LocalSettingsComposerSettings.cs`:

```csharp
using Windows.Storage;

namespace Transom.App.Services;

public sealed class LocalSettingsComposerSettings : IComposerSettings
{
    private const string PostAsDraftKey = "ComposerPostAsDraft";

#if DEBUG
    private const bool DefaultPostAsDraft = true;
#else
    private const bool DefaultPostAsDraft = false;
#endif

    public bool PostAsDraft
    {
        get => ApplicationData.Current.LocalSettings.Values.TryGetValue(PostAsDraftKey, out var value) && value is bool stored
            ? stored
            : DefaultPostAsDraft;
        set => ApplicationData.Current.LocalSettings.Values[PostAsDraftKey] = value;
    }
}
```

- [ ] **Step 3: Document the toggle in SPEC.md**

Edit `SPEC.md` §4.4, adding a bullet after the existing settings list:

```markdown
- **Post as draft (safe testing)** — sends `post-status=draft` instead of publishing live;
  Debug builds default this on so manual smoke testing never creates public posts. Toggle it
  off to test a real publish.
```

- [ ] **Step 4: Build**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" build Transom.sln -c Debug -p:Platform=x64
```

- [ ] **Step 5: Format check**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" format Transom.sln --verify-no-changes
```

- [ ] **Step 6: Commit**

```bash
git add src/Transom.App/Services/IComposerSettings.cs src/Transom.App/Services/LocalSettingsComposerSettings.cs SPEC.md
git commit -m "$(cat <<'EOF'
feat: add Debug-default draft-safety setting (#2)

IComposerSettings.PostAsDraft defaults true in Debug builds so
manual testing against a real account creates drafts, not public
posts; documented in SPEC.md §4.4. Wired into DI and a Settings
toggle in Task 11.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 9: `tests/Transom.App.Tests` scaffold + `ComposerViewModel`

**Files:**
- Create: `tests/Transom.App.Tests/Transom.App.Tests.csproj`
- Modify: `Transom.sln` (add the project; copy the App project's config-platform mapping pattern)
- Modify: `.github/workflows/ci.yml` (add a test step to `build-windows`)
- Create: `src/Transom.App/ViewModels/ComposerViewModel.cs`
- Create: `tests/Transom.App.Tests/TestDoubles/FakeBlogProvider.cs`
- Create: `tests/Transom.App.Tests/TestDoubles/FakeComposerSettings.cs`
- Create: `tests/Transom.App.Tests/ViewModels/ComposerViewModelTests.cs`

**Interfaces:**
- Consumes: `Transom.Core.Providers.IBlogProvider`, `Transom.Core.Models.{PostDraft,
  PublishResult}` (Task 5/4), `Transom.App.Services.IComposerSettings` (Task 8).
- Produces: `Transom.App.ViewModels.ComposerViewModel` with constructor
  `ComposerViewModel(IBlogProvider provider, IComposerSettings settings)`; observable properties
  `Text`, `Title`, `IsPublishing`, `ErrorMessage`, `PublishedUrl`; computed `CharacterCount`,
  `ShowTitleField`; `IAsyncRelayCommand PublishCommand`. Task 11 binds this in XAML.

- [ ] **Step 1: Add package references to `Transom.App` and `Transom.Core` project reference**

`CommunityToolkit.Mvvm` isn't referenced yet anywhere. Edit `src/Transom.App/Transom.App.csproj`,
adding to the existing `PackageReference` `<ItemGroup>`:

```xml
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
```

- [ ] **Step 2: Scaffold `tests/Transom.App.Tests`**

```powershell
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
Set-Location tests
& $dotnet new xunit -n Transom.App.Tests
Set-Location ..
```

Edit `tests/Transom.App.Tests/Transom.App.Tests.csproj` to match `Transom.App`'s TFM (a test
project referencing a WinUI app must target the same Windows TFM) and reference `Transom.App`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0-windows10.0.26100.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <UseWinUI>true</UseWinUI>
    <Platforms>x86;x64;ARM64</Platforms>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Transom.App\Transom.App.csproj" />
  </ItemGroup>

</Project>
```

Delete the template's placeholder test:

```powershell
Remove-Item -Force tests\Transom.App.Tests\UnitTest1.cs
```

- [ ] **Step 3: Add the project to `Transom.sln` with the same platform mapping as `Transom.App`**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" sln Transom.sln add tests\Transom.App.Tests\Transom.App.Tests.csproj
```

Open `Transom.sln` and check the new project's GUID got `ActiveCfg`/`Build.0` lines for
`Debug|Any CPU`, `Debug|x64`, `Debug|x86`, `Release|Any CPU`, `Release|x64`, `Release|x86` (`dotnet
sln add` generates these automatically, mapping the same way `Transom.App`'s did — `Any CPU` maps
to `x86`). **Do not add `Deploy.0` lines for it** — per CLAUDE.md's new note (added in the
Deploy.0 fix), only `Transom.App` itself needs those; a test project isn't deployed.

- [ ] **Step 4: Wire it into CI**

Edit `.github/workflows/ci.yml`, adding a step to the `build-windows` job right after the existing
`Test` step:

```yaml
      - name: Test
        run: dotnet test tests/Transom.Core.Tests --no-restore

      - name: Test App
        run: dotnet test tests/Transom.App.Tests -p:Platform=x64 --no-restore
```

- [ ] **Step 5: Build to confirm the scaffold compiles before writing any tests**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" build Transom.sln -c Debug -p:Platform=x64
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 6: Write the test doubles**

Write `tests/Transom.App.Tests/TestDoubles/FakeBlogProvider.cs`:

```csharp
using Transom.Core.Models;
using Transom.Core.Providers;

namespace Transom.App.Tests.TestDoubles;

internal sealed class FakeBlogProvider : IBlogProvider
{
    public string Id => "fake";

    public Func<PostDraft, CancellationToken, Task<PublishResult>>? OnPublish { get; set; }

    public PostDraft? LastDraft { get; private set; }

    public Task<IReadOnlyList<BlogInfo>> GetBlogsAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<BlogInfo>>([]);

    public Task<PublishResult> PublishAsync(PostDraft draft, CancellationToken cancellationToken)
    {
        LastDraft = draft;
        return OnPublish?.Invoke(draft, cancellationToken)
            ?? Task.FromResult(new PublishResult("https://example.micro.blog/post.html", null));
    }
}
```

Write `tests/Transom.App.Tests/TestDoubles/FakeComposerSettings.cs`:

```csharp
using Transom.App.Services;

namespace Transom.App.Tests.TestDoubles;

internal sealed class FakeComposerSettings : IComposerSettings
{
    public bool PostAsDraft { get; set; }
}
```

- [ ] **Step 7: Write the failing `ComposerViewModel` tests**

Write `tests/Transom.App.Tests/ViewModels/ComposerViewModelTests.cs`:

```csharp
using Transom.App.Tests.TestDoubles;
using Transom.App.ViewModels;
using Transom.Core.MicroBlog;
using Transom.Core.Models;

namespace Transom.App.Tests.ViewModels;

public class ComposerViewModelTests
{
    [Fact]
    public void CharacterCount_ReflectsText()
    {
        var vm = BuildViewModel();

        vm.Text = "Hello";

        Assert.Equal(5, vm.CharacterCount);
    }

    [Fact]
    public void ShowTitleField_HiddenAt300Characters_VisibleAt301()
    {
        var vm = BuildViewModel();

        vm.Text = new string('x', 300);
        Assert.False(vm.ShowTitleField);

        vm.Text = new string('x', 301);
        Assert.True(vm.ShowTitleField);
    }

    [Fact]
    public void PublishCommand_Disabled_WhenTextEmpty()
    {
        var vm = BuildViewModel();

        vm.Text = string.Empty;

        Assert.False(vm.PublishCommand.CanExecute(null));
    }

    [Fact]
    public async Task PublishCommand_Disabled_WhileInFlight()
    {
        var provider = new FakeBlogProvider();
        var gate = new TaskCompletionSource();
        provider.OnPublish = async (_, _) =>
        {
            await gate.Task;
            return new PublishResult("https://example.micro.blog/post.html", null);
        };
        var vm = new ComposerViewModel(provider, new FakeComposerSettings()) { Text = "Hello" };

        var publishTask = vm.PublishCommand.ExecuteAsync(null);
        Assert.False(vm.PublishCommand.CanExecute(null));

        gate.SetResult();
        await publishTask;

        Assert.True(vm.PublishCommand.CanExecute(null));
    }

    [Fact]
    public async Task PublishCommand_OnFailure_KeepsText_AndSetsErrorMessage()
    {
        var provider = new FakeBlogProvider
        {
            OnPublish = (_, _) => throw new MicropubException(System.Net.HttpStatusCode.Unauthorized, "App token was not valid."),
        };
        var vm = new ComposerViewModel(provider, new FakeComposerSettings()) { Text = "Don't lose me" };

        await vm.PublishCommand.ExecuteAsync(null);

        Assert.Equal("Don't lose me", vm.Text);
        Assert.Equal("App token was not valid.", vm.ErrorMessage);
        Assert.Null(vm.PublishedUrl);
    }

    [Fact]
    public async Task PublishCommand_OnSuccess_ClearsTextAndSetsPublishedUrl()
    {
        var provider = new FakeBlogProvider();
        var vm = new ComposerViewModel(provider, new FakeComposerSettings()) { Text = "Hello, world!" };

        await vm.PublishCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, vm.Text);
        Assert.Equal("https://example.micro.blog/post.html", vm.PublishedUrl);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task PublishCommand_PassesDraftFlagFromSettings()
    {
        var provider = new FakeBlogProvider();
        var settings = new FakeComposerSettings { PostAsDraft = true };
        var vm = new ComposerViewModel(provider, settings) { Text = "Hello" };

        await vm.PublishCommand.ExecuteAsync(null);

        Assert.True(provider.LastDraft!.PostAsDraft);
    }

    private static ComposerViewModel BuildViewModel() => new(new FakeBlogProvider(), new FakeComposerSettings());
}
```

- [ ] **Step 8: Run to verify it fails**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test tests\Transom.App.Tests -p:Platform=x64 --filter ComposerViewModelTests
```

Expected: FAIL — `ComposerViewModel` doesn't exist (CS0246).

- [ ] **Step 9: Write `ComposerViewModel`**

Write `src/Transom.App/ViewModels/ComposerViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Transom.App.Services;
using Transom.Core.Models;
using Transom.Core.Providers;

namespace Transom.App.ViewModels;

/// <summary>Composer state and publish logic. No WinUI types (CLAUDE.md Rule 5) — unit tested in
/// <c>Transom.App.Tests</c>.</summary>
public sealed partial class ComposerViewModel : ObservableObject
{
    /// <summary>SPEC.md §4.2: the Title field appears once the post exceeds Micro.blog's
    /// short/long post threshold.</summary>
    private const int TitleThreshold = 300;

    private readonly IBlogProvider _provider;
    private readonly IComposerSettings _settings;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CharacterCount))]
    [NotifyPropertyChangedFor(nameof(ShowTitleField))]
    [NotifyCanExecuteChangedFor(nameof(PublishCommand))]
    private string _text = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PublishCommand))]
    private bool _isPublishing;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _publishedUrl;

    public int CharacterCount => Text.Length;

    public bool ShowTitleField => Text.Length > TitleThreshold;

    public ComposerViewModel(IBlogProvider provider, IComposerSettings settings)
    {
        _provider = provider;
        _settings = settings;
    }

    private bool CanPublish() => !IsPublishing && !string.IsNullOrWhiteSpace(Text);

    [RelayCommand(CanExecute = nameof(CanPublish))]
    private async Task PublishAsync(CancellationToken cancellationToken)
    {
        IsPublishing = true;
        ErrorMessage = null;
        try
        {
            var draft = new PostDraft(Text, ShowTitleField ? Title : null, _settings.PostAsDraft);
            var result = await _provider.PublishAsync(draft, cancellationToken).ConfigureAwait(true);
            PublishedUrl = result.Url;
            Text = string.Empty;
            Title = string.Empty;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsPublishing = false;
        }
    }
}
```

`[NotifyCanExecuteChangedFor(nameof(PublishCommand))]` on `_text`/`_isPublishing` makes
CommunityToolkit.Mvvm's source generator call `PublishCommand.NotifyCanExecuteChanged()` whenever
either changes, so `CanPublish()` is re-evaluated automatically (no manual wiring). "Retry" in the
UI (Task 11) reuses `PublishCommand` — after a failure, `Text` is untouched, so clicking Publish
again (or the Retry button bound to the same command) just retries with the same content.

- [ ] **Step 10: Run to verify it passes**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test tests\Transom.App.Tests -p:Platform=x64 --filter ComposerViewModelTests
```

Expected: `Passed! ... Total: 7`.

- [ ] **Step 11: Full build + format check (both test projects)**

```powershell
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
& $dotnet build Transom.sln -c Debug -p:Platform=x64
& $dotnet test tests\Transom.Core.Tests
& $dotnet test tests\Transom.App.Tests -p:Platform=x64
& $dotnet format Transom.sln --verify-no-changes
```

- [ ] **Step 12: Commit**

```bash
git add Transom.sln .github/workflows/ci.yml src/Transom.App/Transom.App.csproj tests/Transom.App.Tests src/Transom.App/ViewModels/ComposerViewModel.cs
git commit -m "$(cat <<'EOF'
feat: add Transom.App.Tests and ComposerViewModel (#2)

New Windows-only xUnit project (view models need Transom.App's TFM)
wired into the build-windows CI job. ComposerViewModel has no WinUI
types (CLAUDE.md Rule 5); Retry reuses PublishCommand since Text
survives a failed publish.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 10: `SettingsViewModel`

**Files:**
- Create: `src/Transom.Core/Credentials/CredentialAccounts.cs`
- Create: `src/Transom.App/ViewModels/SettingsViewModel.cs`
- Create: `tests/Transom.App.Tests/ViewModels/SettingsViewModelTests.cs`

**Interfaces:**
- Consumes: `Transom.Core.MicroBlog.AccountClient` (Task 3), `Transom.Core.Credentials.{ICredentialStore,
  InMemoryCredentialStore}` (Tasks 5/6).
- Produces: `Transom.Core.Credentials.CredentialAccounts.Default` (the constant `"default"` —
  single account for v1, SPEC §8), `Transom.App.ViewModels.SettingsViewModel` with constructor
  `SettingsViewModel(AccountClient accountClient, ICredentialStore credentialStore)`; observable
  properties `TokenInput`, `IsVerifying`, `ErrorMessage`, `AccountName`, `AccountUsername`,
  `AvatarUrl`; `IAsyncRelayCommand VerifyCommand`. Task 11 binds this in XAML.

- [ ] **Step 1: Write the shared account-id constant**

Write `src/Transom.Core/Credentials/CredentialAccounts.cs`:

```csharp
namespace Transom.Core.Credentials;

/// <summary>Account identifiers used as the <c>ICredentialStore</c> key. Transom v1 supports one
/// Micro.blog account (SPEC.md §8); multiple accounts are out of scope for v1.</summary>
public static class CredentialAccounts
{
    public const string Default = "default";
}
```

Edit `tests/Transom.App.Tests/TestDoubles/FakeBlogProvider.cs`: no change needed (doesn't
reference the account id). Edit `tests/Transom.Core.Tests/Providers/MicroBlogProviderTests.cs`'s
`AccountId` field is a local test constant, not required to change (it's still literally
`"default"`) — leave as-is; this is only called out so the step list doesn't imply a required edit.

- [ ] **Step 2: Write the failing tests**

Write `tests/Transom.App.Tests/ViewModels/SettingsViewModelTests.cs`:

```csharp
using System.Net;
using Transom.App.ViewModels;
using Transom.Core.Credentials;
using Transom.Core.MicroBlog;

namespace Transom.App.Tests.ViewModels;

public class SettingsViewModelTests
{
    [Fact]
    public void VerifyCommand_Disabled_WhenTokenInputEmpty()
    {
        var vm = new SettingsViewModel(BuildAccountClient(_ => throw new InvalidOperationException()), new InMemoryCredentialStore());

        vm.TokenInput = string.Empty;

        Assert.False(vm.VerifyCommand.CanExecute(null));
    }

    [Fact]
    public async Task VerifyCommand_OnSuccess_PopulatesProfileAndStoresToken()
    {
        var credentialStore = new InMemoryCredentialStore();
        var vm = new SettingsViewModel(
            BuildAccountClient(token => Task.FromResult(new Transom.Core.Models.AccountInfo(token + "-verified", "Test User", "testuser", "https://micro.blog/testuser/avatar.jpg", "testuser.micro.blog", null))),
            credentialStore)
        {
            TokenInput = "pasted-token",
        };

        await vm.VerifyCommand.ExecuteAsync(null);

        Assert.Equal("Test User", vm.AccountName);
        Assert.Equal("testuser", vm.AccountUsername);
        Assert.Equal("https://micro.blog/testuser/avatar.jpg", vm.AvatarUrl);
        Assert.Null(vm.ErrorMessage);
        Assert.Equal("pasted-token-verified", credentialStore.TryGet(CredentialAccounts.Default));
    }

    [Fact]
    public async Task VerifyCommand_OnFailure_ShowsErrorAndDoesNotStoreToken()
    {
        var credentialStore = new InMemoryCredentialStore();
        var vm = new SettingsViewModel(
            BuildAccountClient(_ => throw new MicropubException(HttpStatusCode.Unauthorized, "App token was not valid.")),
            credentialStore)
        {
            TokenInput = "bad-token",
        };

        await vm.VerifyCommand.ExecuteAsync(null);

        Assert.Equal("App token was not valid.", vm.ErrorMessage);
        Assert.Null(credentialStore.TryGet(CredentialAccounts.Default));
        Assert.Null(vm.AccountName);
    }

    [Fact]
    public async Task VerifyCommand_Disabled_WhileVerifying()
    {
        var gate = new TaskCompletionSource();
        var vm = new SettingsViewModel(
            BuildAccountClient(async token => { await gate.Task; return new Transom.Core.Models.AccountInfo(token, "Test User", "testuser", null, null, null); }),
            new InMemoryCredentialStore())
        {
            TokenInput = "pasted-token",
        };

        var verifyTask = vm.VerifyCommand.ExecuteAsync(null);
        Assert.False(vm.VerifyCommand.CanExecute(null));

        gate.SetResult();
        await verifyTask;

        Assert.True(vm.VerifyCommand.CanExecute(null));
    }

    private static AccountClient BuildAccountClient(Func<string, Task<Transom.Core.Models.AccountInfo>> verify)
    {
        var handler = new DelegatingVerifyHandler(verify);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://micro.blog") };
        return new AccountClient(httpClient, new MicropubClient(httpClient));
    }

    /// <summary>Routes /account/verify to the test's callback so SettingsViewModel tests don't
    /// need real HTTP fixtures — AccountClient itself is already covered by Task 3's tests.</summary>
    private sealed class DelegatingVerifyHandler : HttpMessageHandler
    {
        private readonly Func<string, Task<Transom.Core.Models.AccountInfo>> _verify;

        public DelegatingVerifyHandler(Func<string, Task<Transom.Core.Models.AccountInfo>> verify) => _verify = verify;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var form = await request.Content!.ReadAsStringAsync(cancellationToken);
            var token = System.Web.HttpUtility.ParseQueryString(form)["token"]!;

            try
            {
                var account = await _verify(token);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = System.Net.Http.Json.JsonContent.Create(account, Transom.Core.MicroBlog.TransomJsonContext.Default.AccountInfo),
                };
            }
            catch (MicropubException ex)
            {
                return new HttpResponseMessage(ex.StatusCode)
                {
                    Content = System.Net.Http.Json.JsonContent.Create(new Transom.Core.Models.ApiErrorResponse(ex.Message), Transom.Core.MicroBlog.TransomJsonContext.Default.ApiErrorResponse),
                };
            }
        }
    }
}
```

`TransomJsonContext` is `internal` (Task 1); this compiles here because
[assembly: InternalsVisibleTo] isn't set up and doesn't need to be — `Transom.App.Tests` doesn't
reference `Transom.Core.MicroBlog.TransomJsonContext` in this file, it's used the same way
production code uses it (as a public static member `.Default.X` off an otherwise-internal type is
**not** accessible cross-assembly). **Correction while writing this test:** since
`TransomJsonContext` is `internal` to `Transom.Core`, `Transom.App.Tests` cannot reference
`TransomJsonContext.Default.AccountInfo` directly. Use `System.Net.Http.Json.JsonContent.Create`
with a plain `JsonSerializerOptions` instead — replace both `JsonContent.Create` calls above with:

```csharp
return new HttpResponseMessage(HttpStatusCode.OK)
{
    Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(account), System.Text.Encoding.UTF8, "application/json"),
};
```

and for the error case:

```csharp
return new HttpResponseMessage(ex.StatusCode)
{
    Content = new StringContent($$"""{"error":"{{ex.Message}}"}""", System.Text.Encoding.UTF8, "application/json"),
};
```

(Reflection-based `JsonSerializer.Serialize(account)` works fine here since it's test-only code,
not subject to Core's warnings-as-errors trim/AOT posture.)

- [ ] **Step 3: Run to verify it fails**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test tests\Transom.App.Tests -p:Platform=x64 --filter SettingsViewModelTests
```

Expected: FAIL — `SettingsViewModel` doesn't exist (CS0246).

- [ ] **Step 4: Write `SettingsViewModel`**

Write `src/Transom.App/ViewModels/SettingsViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Transom.Core.Credentials;
using Transom.Core.MicroBlog;

namespace Transom.App.ViewModels;

/// <summary>Token entry, verification and profile display (SPEC.md §4.4). No WinUI types
/// (CLAUDE.md Rule 5) — unit tested in <c>Transom.App.Tests</c>.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AccountClient _accountClient;
    private readonly ICredentialStore _credentialStore;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerifyCommand))]
    private string _tokenInput = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerifyCommand))]
    private bool _isVerifying;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _accountName;

    [ObservableProperty]
    private string? _accountUsername;

    [ObservableProperty]
    private string? _avatarUrl;

    public SettingsViewModel(AccountClient accountClient, ICredentialStore credentialStore)
    {
        _accountClient = accountClient;
        _credentialStore = credentialStore;
    }

    private bool CanVerify() => !IsVerifying && !string.IsNullOrWhiteSpace(TokenInput);

    [RelayCommand(CanExecute = nameof(CanVerify))]
    private async Task VerifyAsync(CancellationToken cancellationToken)
    {
        IsVerifying = true;
        ErrorMessage = null;
        try
        {
            var account = await _accountClient.VerifyAsync(TokenInput, cancellationToken).ConfigureAwait(true);
            _credentialStore.Save(CredentialAccounts.Default, account.Token);
            AccountName = account.Name;
            AccountUsername = account.Username;
            AvatarUrl = account.Avatar;
            TokenInput = string.Empty;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsVerifying = false;
        }
    }
}
```

- [ ] **Step 5: Run to verify it passes**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test tests\Transom.App.Tests -p:Platform=x64 --filter SettingsViewModelTests
```

Expected: `Passed! ... Total: 4`.

- [ ] **Step 6: Full build + format check**

```powershell
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
& $dotnet build Transom.sln -c Debug -p:Platform=x64
& $dotnet test tests\Transom.Core.Tests
& $dotnet test tests\Transom.App.Tests -p:Platform=x64
& $dotnet format Transom.sln --verify-no-changes
```

- [ ] **Step 7: Commit**

```bash
git add src/Transom.Core/Credentials/CredentialAccounts.cs src/Transom.App/ViewModels/SettingsViewModel.cs tests/Transom.App.Tests/ViewModels/SettingsViewModelTests.cs
git commit -m "$(cat <<'EOF'
feat: add SettingsViewModel (#2)

Verifies a pasted token via AccountClient directly (not through
IBlogProvider — nothing is stored yet to configure a provider
instance with), stores the token /account/verify returns (which can
differ from what was typed), and surfaces name/username/avatar.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 11: DI wiring and XAML pages

**Files:**
- Modify: `src/Transom.App/HostBuilderExtensions.cs` (typed clients, credential store, providers,
  view models)
- Create: `src/Transom.App/Views/ComposerPage.xaml` and `.xaml.cs`
- Create: `src/Transom.App/Views/SettingsPage.xaml` and `.xaml.cs`
- Modify: `src/Transom.App/MainWindow.xaml` and `.xaml.cs` (navigation, New post, content frame)

**Interfaces:**
- Consumes: everything from Tasks 1–10.
- Produces: a running app where Settings → paste token → verify shows the profile, and the
  composer publishes text with the character count/Title/Retry behavior. Nothing downstream in
  this milestone consumes new types from this task — it's integration.

- [ ] **Step 1: Rewire `HostBuilderExtensions`**

Edit `src/Transom.App/HostBuilderExtensions.cs`:

```csharp
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
```

`MicroBlogHttpClientName` (M0) is gone: `MicropubClient` and `AccountClient` are now typed
clients, each with its own `HttpClient` configured for its own consumer — simpler than passing a
bare named client around once there were real consumers to configure it for.

- [ ] **Step 2: Write `ComposerPage`**

Write `src/Transom.App/Views/ComposerPage.xaml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<Page
    x:Class="Transom.App.Views.ComposerPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    mc:Ignorable="d">
    <Page.Resources>
        <KeyboardAccelerator x:Key="PublishAccelerator" Key="Enter" Modifiers="Control" Invoked="PublishAccelerator_Invoked" />
    </Page.Resources>
    <Grid Padding="24" KeyboardAccelerators="{StaticResource PublishAccelerator}">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <TextBox
            x:Name="TitleBox"
            Grid.Row="0"
            Margin="0,0,0,8"
            PlaceholderText="Title"
            AutomationProperties.Name="Post title"
            Text="{x:Bind ViewModel.Title, Mode=TwoWay}"
            Visibility="{x:Bind ViewModel.ShowTitleField, Mode=OneWay}" />

        <TextBox
            Grid.Row="1"
            AcceptsReturn="True"
            TextWrapping="Wrap"
            PlaceholderText="What's on your mind?"
            AutomationProperties.Name="Post text"
            Text="{x:Bind ViewModel.Text, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />

        <TextBlock
            Grid.Row="2"
            Margin="0,8,0,8"
            HorizontalAlignment="Right"
            Opacity="0.7"
            Text="{x:Bind ViewModel.CharacterCount, Mode=OneWay}" />

        <StackPanel Grid.Row="3" Orientation="Horizontal" Spacing="12">
            <Button
                Content="Publish"
                AutomationProperties.Name="Publish post"
                Command="{x:Bind ViewModel.PublishCommand}" />
            <ProgressRing IsActive="{x:Bind ViewModel.IsPublishing, Mode=OneWay}" Width="20" Height="20" />
        </StackPanel>

        <InfoBar
            Grid.Row="3"
            Margin="0,48,0,0"
            IsOpen="{x:Bind ViewModel.PublishedUrl, Mode=OneWay, Converter={StaticResource NullToBoolConverter}}"
            Severity="Success"
            Title="Published"
            Message="{x:Bind ViewModel.PublishedUrl, Mode=OneWay}" />

        <InfoBar
            Grid.Row="3"
            Margin="0,48,0,0"
            IsOpen="{x:Bind ViewModel.ErrorMessage, Mode=OneWay, Converter={StaticResource NullToBoolConverter}}"
            Severity="Error"
            Title="Couldn't publish"
            Message="{x:Bind ViewModel.ErrorMessage, Mode=OneWay}">
            <InfoBar.ActionButton>
                <Button Content="Retry" AutomationProperties.Name="Retry publish" Command="{x:Bind ViewModel.PublishCommand}" />
            </InfoBar.ActionButton>
        </InfoBar>
    </Grid>
</Page>
```

Write `src/Transom.App/Views/ComposerPage.xaml.cs`:

```csharp
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Transom.App.ViewModels;

namespace Transom.App.Views;

public sealed partial class ComposerPage : Page
{
    public ComposerViewModel ViewModel { get; }

    public ComposerPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<ComposerViewModel>();
        InitializeComponent();
    }

    private void PublishAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ViewModel.PublishCommand.CanExecute(null))
        {
            ViewModel.PublishCommand.Execute(null);
        }
        args.Handled = true;
    }
}
```

`NullToBoolConverter` doesn't exist yet — add it now since both `InfoBar`s need it. Write
`src/Transom.App/Converters/NullToBoolConverter.cs`:

```csharp
using Microsoft.UI.Xaml.Data;

namespace Transom.App.Converters;

/// <summary>True when the bound value is a non-null, non-empty string — used to drive an
/// InfoBar's IsOpen from a nullable status message.</summary>
public sealed class NullToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => !string.IsNullOrEmpty(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
```

Register it as a page resource: edit `ComposerPage.xaml`'s `<Page.Resources>` to also declare it
(add above the `KeyboardAccelerator`):

```xml
<Page.Resources>
    <converters:NullToBoolConverter x:Key="NullToBoolConverter" />
    <KeyboardAccelerator x:Key="PublishAccelerator" Key="Enter" Modifiers="Control" Invoked="PublishAccelerator_Invoked" />
</Page.Resources>
```

and add the namespace to the `<Page>` root tag: `xmlns:converters="using:Transom.App.Converters"`.

- [ ] **Step 3: Write `SettingsPage`**

Write `src/Transom.App/Views/SettingsPage.xaml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<Page
    x:Class="Transom.App.Views.SettingsPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    mc:Ignorable="d">
    <StackPanel Padding="24" Spacing="12" MaxWidth="480" HorizontalAlignment="Left">
        <TextBlock Text="Micro.blog account" Style="{StaticResource SubtitleTextBlockStyle}" />

        <StackPanel Orientation="Horizontal" Spacing="12" Visibility="{x:Bind ViewModel.AccountUsername, Mode=OneWay, Converter={StaticResource NullToBoolConverter}}">
            <PersonPicture ProfilePicture="{x:Bind ViewModel.AvatarUrl, Mode=OneWay}" Width="40" Height="40" />
            <StackPanel>
                <TextBlock Text="{x:Bind ViewModel.AccountName, Mode=OneWay}" FontWeight="SemiBold" />
                <TextBlock Text="{x:Bind ViewModel.AccountUsername, Mode=OneWay}" Opacity="0.7" />
            </StackPanel>
        </StackPanel>

        <TextBox
            PlaceholderText="Paste your Micro.blog app token"
            AutomationProperties.Name="App token"
            Text="{x:Bind ViewModel.TokenInput, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />

        <StackPanel Orientation="Horizontal" Spacing="12">
            <Button
                Content="Verify"
                AutomationProperties.Name="Verify app token"
                Command="{x:Bind ViewModel.VerifyCommand}" />
            <ProgressRing IsActive="{x:Bind ViewModel.IsVerifying, Mode=OneWay}" Width="20" Height="20" />
        </StackPanel>

        <InfoBar
            IsOpen="{x:Bind ViewModel.ErrorMessage, Mode=OneWay, Converter={StaticResource NullToBoolConverter}}"
            Severity="Error"
            Title="Couldn't verify"
            Message="{x:Bind ViewModel.ErrorMessage, Mode=OneWay}" />

        <ToggleSwitch
            Header="Post as draft (safe testing)"
            AutomationProperties.Name="Post as draft"
            IsOn="{x:Bind PostAsDraft, Mode=TwoWay}" />
    </StackPanel>
</Page>
```

Write `src/Transom.App/Views/SettingsPage.xaml.cs`:

```csharp
using Microsoft.UI.Xaml.Controls;
using Transom.App.Services;
using Transom.App.ViewModels;

namespace Transom.App.Views;

public sealed partial class SettingsPage : Page
{
    private readonly IComposerSettings _composerSettings;

    public SettingsViewModel ViewModel { get; }

    public bool PostAsDraft
    {
        get => _composerSettings.PostAsDraft;
        set => _composerSettings.PostAsDraft = value;
    }

    public SettingsPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<SettingsViewModel>();
        _composerSettings = App.Host.Services.GetRequiredService<IComposerSettings>();
        InitializeComponent();
    }
}
```

Add the same converter resource declaration used in `ComposerPage.xaml` to `SettingsPage.xaml`'s
root (`<Page.Resources><converters:NullToBoolConverter x:Key="NullToBoolConverter" /></Page.Resources>`,
with the matching `xmlns:converters="using:Transom.App.Converters"` on `<Page>`).

- [ ] **Step 4: Wire navigation into `MainWindow`**

Edit `src/Transom.App/MainWindow.xaml`, replacing the empty `NavigationView` with one that has a
content `Frame`, a "New post" item, and a Settings footer item:

```xml
        <NavigationView
            x:Name="NavView"
            Grid.Row="1"
            AutomationProperties.Name="Main navigation"
            IsBackButtonVisible="Collapsed"
            IsPaneToggleButtonVisible="False"
            ItemInvoked="NavView_ItemInvoked">
            <NavigationView.MenuItems>
                <NavigationViewItem Content="New post" Tag="Composer" AutomationProperties.Name="New post">
                    <NavigationViewItem.Icon>
                        <SymbolIcon Symbol="Add" />
                    </NavigationViewItem.Icon>
                </NavigationViewItem>
            </NavigationView.MenuItems>
            <NavigationView.FooterMenuItems>
                <NavigationViewItem Content="Settings" Tag="Settings" AutomationProperties.Name="Settings">
                    <NavigationViewItem.Icon>
                        <SymbolIcon Symbol="Setting" />
                    </NavigationViewItem.Icon>
                </NavigationViewItem>
            </NavigationView.FooterMenuItems>
            <Frame x:Name="ContentFrame" />
        </NavigationView>
```

Edit `src/Transom.App/MainWindow.xaml.cs`:

```csharp
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Transom.App.Views;

namespace Transom.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");

        ContentFrame.Navigate(typeof(ComposerPage));
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        var tag = (args.InvokedItemContainer as NavigationViewItem)?.Tag as string;
        ContentFrame.Navigate(tag switch
        {
            "Settings" => typeof(SettingsPage),
            _ => typeof(ComposerPage),
        });
    }
}
```

M1 has no Timeline/Mentions/Discover/Bookmarks/My posts/Drafts pages yet (those start in M3–M5
per `docs/milestones.md`), so the app opens straight to the composer — SPEC §4.1's "persistent New
post button" — rather than to an empty Timeline placeholder.

- [ ] **Step 5: Build**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" build Transom.sln -c Debug -p:Platform=x64
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`. Fix any XAML binding/namespace typos from the
hand-written markup above before moving on — this step is expected to need a couple of iterations.

- [ ] **Step 6: Format check**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" format Transom.sln --verify-no-changes
```

- [ ] **Step 7: Run both test suites once more (this task touches `HostBuilderExtensions`, which nothing tests directly, but confirms nothing else broke)**

```powershell
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
& $dotnet test tests\Transom.Core.Tests
& $dotnet test tests\Transom.App.Tests -p:Platform=x64
```

- [ ] **Step 8: Commit**

```bash
git add src/Transom.App/HostBuilderExtensions.cs src/Transom.App/Views src/Transom.App/Converters src/Transom.App/MainWindow.xaml src/Transom.App/MainWindow.xaml.cs
git commit -m "$(cat <<'EOF'
feat: wire composer and settings into the app shell (#2)

DI now resolves MicropubClient/AccountClient as typed clients,
ICredentialStore to PasswordVaultCredentialStore, IBlogProvider to
MicroBlogProvider. NavigationView gets New post + Settings; the app
opens to the composer since M1 has no Timeline yet.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 12: Manual smoke test, milestones, CLAUDE.md

**Files:**
- Modify: `docs/milestones.md` (tick M1 boxes)
- Modify: `CLAUDE.md` (Commands: add the new test project; Layout: mention it)

**Interfaces:**
- Consumes: everything.
- Produces: nothing consumed by later tasks — this is M1's terminal task.

- [ ] **Step 1: Manual smoke test against your real account**

Run the app (F5, Transom.App (Package) profile, or `dotnet run --project src/Transom.App
-p:Platform=x64`). In Settings:
1. Confirm the "Post as draft (safe testing)" toggle is **on** by default (Debug build).
2. Paste a real Micro.blog app token, click Verify. Confirm your name/username/avatar appear.
3. Go to the composer, type a short post, publish. Confirm the success InfoBar shows a link, and
   that the post appears in micro.blog's dashboard as a **draft** (not live) — then delete it
   there.
4. Type text longer than 300 characters; confirm the Title field appears.
5. Turn off "Post as draft", publish one real short test post, confirm it's live on your blog,
   then delete it from micro.blog if you don't want it kept.
6. Disconnect from the network (or use an obviously-wrong token) and try to publish; confirm the
   text stays in the box, an error InfoBar with Retry appears, and clicking Retry attempts again.

**While doing this, revisit the two "unconfirmed" discovery notes at the top of this plan**: check
your real account's `GET /micropub?q=config` response shape (e.g. via `curl -H "Authorization:
Bearer <token>" https://micro.blog/micropub?q=config`) against `config-one-blog.json`'s assumed
shape, and note in the PR description if it differs (update the fixture/model in a follow-up if
so — no provider code change should be needed either way, per the discovery note).

- [ ] **Step 2: Tick the M1 boxes**

Edit `docs/milestones.md`, changing under `## M1 — Sign in and text post (U1, U2)`:

```markdown
- [x] `TransomJsonContext` (source-generated) with the first real DTOs
- [x] `ICredentialStore` + `PasswordVault` implementation
- [x] Settings → paste app token → verify (`/account/verify` or `q=config`) → show avatar/name
- [x] `MicropubClient.GetConfigAsync`, `PublishAsync` (text + optional title)
- [x] Composer: text, character count, auto Title field over 300 chars, Ctrl+Enter publishes
- [x] Success InfoBar with link to the post; failure keeps text and offers Retry
```

- [ ] **Step 3: Update CLAUDE.md**

Edit `CLAUDE.md`'s Commands section, adding the new test project's command:

```markdown
dotnet test tests/Transom.Core.Tests
dotnet test tests/Transom.App.Tests -p:Platform=x64
```

Edit CLAUDE.md's Layout section, updating the `tests/Transom.Core.Tests` line to also mention the
new project:

```markdown
- `tests/Transom.Core.Tests` — xUnit, builds/tests on Linux too.
- `tests/Transom.App.Tests` — xUnit, view-model tests for `Transom.App`; needs the Windows App
  SDK like `Transom.App` does, so it only builds/runs on Windows.
```

- [ ] **Step 4: Final full verification, matching CI exactly**

```powershell
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
& $dotnet restore Transom.sln -p:Platform=x64
& $dotnet build Transom.sln -c Debug -p:Platform=x64 --no-restore
& $dotnet test tests\Transom.Core.Tests --no-restore
& $dotnet test tests\Transom.App.Tests -p:Platform=x64 --no-restore
& $dotnet format Transom.sln --verify-no-changes
```

- [ ] **Step 5: Commit**

```bash
git add docs/milestones.md CLAUDE.md
git commit -m "$(cat <<'EOF'
docs: tick M1 boxes, document Transom.App.Tests in CLAUDE.md (#2)

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 6: Push and confirm CI is green, then close #2**

```bash
git push origin main
gh run watch --exit-status
gh issue close 2 --comment "M1 done: sign-in and text-post publish work end to end, verified manually against a real account plus CI. Continuing in the M2 issue."
```

---

## Self-review

1. **Spec coverage** — U1 (paste token, stay signed in): Tasks 3, 6, 7, 10 (verify + PasswordVault
   storage). U2 (write + publish in under 5s): Tasks 4, 5, 9, 11 (publish path + composer UI).
   SPEC §4.2 composer bullets: character count ✅ (Task 9), Title over 300 chars ✅ (Task 9),
   Ctrl+Enter ✅ (Task 11), "text never lost" ✅ (Task 9 test + Rule 6). SPEC §6.1 auth: confirmed
   and recorded in SPEC.md itself (pre-plan step, not a task — already done). SPEC §6.2 posting:
   config ✅ (Task 2), publish ✅ (Task 4), `post-status=draft` ✅ (Task 4, wired to the safety
   toggle in Task 8/9). SPEC §8 storage: PasswordVault, resource "Transom" ✅ (Task 7); tokens
   never logged ✅ (Task 2/5 tests assert no token leakage into exception messages, and M0's
   `RedactingLoggingHandler` already covers the HTTP layer). Milestone's 6 checklist bullets are
   each covered by at least one task; Task 12 ticks them.
2. **Placeholder scan** — no TBD/TODO. The two deliberately-thin spots (`IBlogProvider` trimmed to
   2 methods; App-layer stores not unit tested) are each explained with a concrete technical
   reason (no caller yet; no package identity in a plain test host) and a concrete alternative
   (defer to the milestone that adds a caller; cover the isolable logic with a fake, verify the
   platform glue manually) — not vague deferrals.
3. **Type consistency** — `PostDraft(string Content, string? Title, bool PostAsDraft)` and
   `PublishResult(string Url, string? PreviewUrl)` (Task 4) are used identically by
   `MicropubClient.PublishAsync`, `MicroBlogProvider.PublishAsync` (Task 5), and
   `ComposerViewModel.PublishAsync` (Task 9). `ICredentialStore`'s three methods (Task 5) are
   implemented identically by `InMemoryCredentialStore` (Task 6) and `PasswordVaultCredentialStore`
   (Task 7), and consumed identically by `MicroBlogProvider` (Task 5) and `SettingsViewModel`
   (Task 10). `AccountClient.VerifyAsync(string token, CancellationToken)` (Task 3) is the same
   signature Task 10's `SettingsViewModel` calls. `CredentialAccounts.Default` (Task 10) is the
   same account id Task 5's tests hardcode as `"default"` and Task 11's DI wiring passes to
   `MicroBlogProvider`.
4. Every fixture's field names trace to a specific, cited help.micro.blog page fetched this
   session; the two genuinely unconfirmed shapes (single-blog `destination`, and 401/500 error
   bodies) are called out by name in the discovery notes with a stated assumption and how to
   verify it, rather than presented as confirmed.
