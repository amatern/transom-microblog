# M0 — Scaffold and CI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up the Transom solution (Core/App/Tests), DI host with Authorization-redacting HTTP logging, style/nullable/warnings enforcement, CI on Windows + Linux, and an empty NavigationView shell — matching `docs/milestones.md` M0.

**Architecture:** Three projects (`src/Transom.Core` net10.0 class library with zero WinUI references, `src/Transom.App` WinUI 3 packaged app via the official `winui-navview` template pruned to an empty shell, `tests/Transom.Core.Tests` xUnit) under one classic-format `Transom.sln`. `Transom.App` hosts a `Microsoft.Extensions.Hosting` generic host that registers logging and a named `HttpClient` whose pipeline includes a `RedactingLoggingHandler` (defined in Core, tested with a fake inner handler) so `Authorization` header values never reach logs.

**Tech Stack:** .NET 10 SDK (10.0.401, confirmed installed via Visual Studio 2026 Community), WinUI 3 / Windows App SDK 2.5.1 (via `Microsoft.WindowsAppSDK.WinUI.CSharp.Templates` 0.0.6-alpha, installed this session with `dotnet new install`), CommunityToolkit.Mvvm (added, unused until M1), Microsoft.Extensions.Hosting/Http/Logging, xUnit.

**Spec:** `SPEC.md` §5 (architecture), `docs/milestones.md` M0.

## Global Constraints

- `src/Transom.Core` must not reference WinUI or any `Windows.*` API and must build/test on `ubuntu-latest` (CLAUDE.md Layout).
- Never log, print, persist or commit tokens; test tokens are obviously fake (`test-token`) (CLAUDE.md Rule 1).
- No real network calls in tests (CLAUDE.md Rule 2).
- Every async method takes a `CancellationToken`; no `.Result`/`.Wait()` (CLAUDE.md Rule 4).
- Nullable reference types on repo-wide; warnings-as-errors in `Transom.Core` specifically (CLAUDE.md Rule 8).
- File-scoped namespaces; `var` when the type is obvious; XML doc comments on public Core APIs (CLAUDE.md Style).
- Conventional commits (`feat:`, `chore:`, `test:`), one per task (CLAUDE.md Commits).

## Discovery notes (from this session's spike, not part of any task — context for the executor)

- No .NET SDK or Visual Studio was installed at session start. The user installed Visual Studio Community 2026 with the ".NET desktop development" and "Windows application development" workloads. `dotnet --version` now reports `10.0.401`, but **this PowerShell tool's cached `PATH` does not pick it up automatically** — every command below must invoke `"C:\Program Files\dotnet\dotnet.exe"` by full path (or refresh `$env:Path` from the registry first), not bare `dotnet`.
- `dotnet new list` shows no WinUI templates out of the box — VS's own "Blank App, Packaged (WinUI 3 in Desktop)" template is VS-internal, not exposed to the CLI. The official CLI-installable pack is `Microsoft.WindowsAppSDK.WinUI.CSharp.Templates` (currently version `0.0.6-alpha` on NuGet, published/verified by Microsoft). Install once with:
  `dotnet new install Microsoft.WindowsAppSDK.WinUI.CSharp.Templates@0.0.6-alpha`
  This provides, among others, `winui-navview` (WinUI NavigationView App) — Mica backdrop, `TitleBar` with pane-toggle, `NavigationView`, single-project MSIX (`EnableMsixTooling`), and a `launchSettings.json` with a `"<Name> (Package)"` profile — which is exactly the profile name CLAUDE.md's Commands section already references. Verified this template restores and builds clean (`dotnet build -p:Platform=x64`, 0 warnings/errors) after pruning its sample `Pages/` folder and simplifying `MainWindow.xaml`/`.xaml.cs` to a genuinely empty shell.
- `dotnet new sln` in this SDK defaults to the new `.slnx` XML format. CLAUDE.md's commands reference `Transom.sln` (classic format), so scaffolding must pass `-f sln` explicitly.
- Template-generated files (from both `classlib`/`xunit`/`winui-navview`) do **not** conform to the repo's target `.editorconfig` out of the box (CRLF/final-newline mismatches) — `dotnet format` (unrestricted) must run once after scaffolding to normalize them before `dotnet format --verify-no-changes` will pass. Verified this two-step sequence converges to a clean, idempotent state.
- Scoping `TreatWarningsAsErrors` to only `Transom.Core` (not `Transom.App`, not `Transom.Core.Tests`) is done with a **nested** `Directory.Build.props` inside `src/Transom.Core/` that explicitly `<Import>`s the root one — MSBuild only auto-imports the *nearest* `Directory.Build.props` per project, so `Transom.App` and `Transom.Core.Tests` (which have no local override) pick up only the root file. Verified: injecting an unused local variable into Core fails the build with `CS0168`; the root-only settings (`Nullable`, `ImplicitUsings`) still apply everywhere.
- `System.Text.Json.Serialization.JsonSerializerContext` **cannot compile with zero `[JsonSerializable]` attributes** — the source generator only emits the required abstract member overrides (`GeneratedSerializerOptions`, `GetTypeInfo`) when at least one type is registered; an attribute-less partial class fails with `CS0534`. Since M0 has no real DTOs yet (Models arrive with M1's Micropub/JSON clients per `SPEC.md` §5), inventing a placeholder type to force it to compile now would violate CLAUDE.md's "only build what the current milestone asks for" and this plan's own no-placeholder rule. **Decision: `TransomJsonContext` is deferred to M1's first task**, created alongside the first real response DTO (e.g. `AccountInfo`). This plan implements the other two M0 items under that bullet — DI host and Authorization-redacting logging — in full. Flag this to the user when the plan finishes; `docs/milestones.md`'s M0 checkbox for that line should stay unchecked until M1 lands the context, or be split, at the user's preference.
- `dotnet build <sln> -c Debug -p:Platform=x64` (CLAUDE.md's exact documented command) was verified end-to-end against the mixed-platform solution (`Transom.App` declares `Platforms=x86;x64;ARM64`; `Transom.Core`/`Transom.Core.Tests` are plain `AnyCPU`) — solution-level platform mapping resolves correctly, 0 warnings/errors.

---

## Task 1: Solution and project scaffold

**Files:**
- Create: `Transom.sln`
- Create: `Directory.Build.props` (repo root)
- Create: `src/Transom.Core/Directory.Build.props`
- Create: `src/Transom.Core/Transom.Core.csproj`
- Create: `src/Transom.App/Transom.App.csproj` and template-generated files (`App.xaml`, `App.xaml.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`, `Package.appxmanifest`, `app.manifest`, `Assets/*`, `.gitignore` (discard — repo root one already covers it), `Properties/launchSettings.json`, `Properties/PublishProfiles/*.pubxml`)
- Create: `tests/Transom.Core.Tests/Transom.Core.Tests.csproj`
- Modify: `.editorconfig` (repo root — currently does not exist; create it)

**Interfaces:**
- Produces: `Transom.Core` project (net10.0, `Nullable=enable`, `TreatWarningsAsErrors=true`) that `Transom.Core.Tests` and (later) `Transom.App` reference.
- Produces: `Transom.App` project (net10.0-windows10.0.26100.0, WinUI 3, packaged) with an empty `NavigationView` shell, Mica backdrop, system light/dark.

- [ ] **Step 1: Install the WinUI CLI templates (one-time, machine-level, not part of repo)**

Run (full path required — this session's shell PATH is stale):

```powershell
& "C:\Program Files\dotnet\dotnet.exe" new install Microsoft.WindowsAppSDK.WinUI.CSharp.Templates@0.0.6-alpha
```

Expected: `Success: Microsoft.WindowsAppSDK.WinUI.CSharp.Templates@0.0.6-alpha installed the following templates:` listing `winui-navview` among others.

- [ ] **Step 2: Scaffold the solution file and three projects**

From the repo root:

```powershell
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
& $dotnet new sln -n Transom -f sln
New-Item -ItemType Directory -Force -Path src, tests | Out-Null
Set-Location src
& $dotnet new classlib -n Transom.Core
& $dotnet new winui-navview -n Transom.App
Set-Location ..\tests
& $dotnet new xunit -n Transom.Core.Tests
Set-Location ..
& $dotnet sln Transom.sln add src\Transom.Core\Transom.Core.csproj src\Transom.App\Transom.App.csproj tests\Transom.Core.Tests\Transom.Core.Tests.csproj
```

Expected: `Transom.sln` created (classic format, not `.slnx`); all three `Project "..."` add lines succeed.

- [ ] **Step 3: Delete the WinUI template's sample content and default test/class files**

```powershell
Remove-Item -Recurse -Force src\Transom.App\Pages
Remove-Item -Force src\Transom.Core\Class1.cs
Remove-Item -Force tests\Transom.Core.Tests\UnitTest1.cs
Remove-Item -Force src\Transom.App\.gitignore  # repo root .gitignore already covers bin/obj/AppPackages/*.msix etc.
```

- [ ] **Step 4: Replace `MainWindow.xaml` with the empty-shell version**

Write `src/Transom.App/MainWindow.xaml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<Window
    x:Class="Transom.App.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:local="using:Transom.App"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    Title="Transom"
    mc:Ignorable="d">
    <Window.SystemBackdrop>
        <MicaBackdrop />
    </Window.SystemBackdrop>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="48" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <TitleBar
            x:Name="AppTitleBar"
            Title="Transom"
            IsPaneToggleButtonVisible="True"
            PaneToggleRequested="TitleBar_PaneToggleRequested">
            <TitleBar.IconSource>
                <ImageIconSource ImageSource="Assets/AppIcon.ico" />
            </TitleBar.IconSource>
        </TitleBar>

        <NavigationView
            x:Name="NavView"
            Grid.Row="1"
            AutomationProperties.Name="Main navigation"
            IsBackButtonVisible="Collapsed"
            IsPaneToggleButtonVisible="False" />
    </Grid>
</Window>
```

- [ ] **Step 5: Replace `MainWindow.xaml.cs` with the matching code-behind**

Write `src/Transom.App/MainWindow.xaml.cs`:

```csharp
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }
}
```

- [ ] **Step 6: Fix up namespaces in the untouched template files**

`dotnet new winui-navview -n Transom.App` already generates `App.xaml`/`App.xaml.cs`/`Package.appxmanifest` using the `Transom.App` / `Transom_App` namespace and `Transom.App` display name (the template derives these from `-n`) — open `src/Transom.App/App.xaml.cs` and confirm the namespace reads `Transom.App` (it does; the template's namespace substitution uses the literal project name, not an underscored variant, when the name has no spaces). No edit needed if so; if the generated namespace is `Transom_App` instead, rename it to `Transom.App` in `App.xaml`, `App.xaml.cs`, `MainWindow.xaml` (already written above), and `Transom.App.csproj`'s `<RootNamespace>`.

- [ ] **Step 7: Create the repo-root `Directory.Build.props`**

Write `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

- [ ] **Step 8: Create the nested `Directory.Build.props` that scopes warnings-as-errors to Core**

Write `src/Transom.Core/Directory.Build.props`:

```xml
<Project>
  <Import Project="$(MSBuildThisFileDirectory)..\..\Directory.Build.props" />
  <PropertyGroup>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

- [ ] **Step 9: Create the root `.editorconfig`**

Generate the SDK's default C# `.editorconfig` and then adjust the two lines CLAUDE.md's Style section calls out explicitly (file-scoped namespaces, `var` when the type is obvious):

```powershell
& "C:\Program Files\dotnet\dotnet.exe" new editorconfig
```

Then edit the generated `.editorconfig`:
- Change `csharp_style_namespace_declarations = file_scoped:suggestion` to `csharp_style_namespace_declarations = file_scoped:warning`.
- Change `csharp_style_var_when_type_is_apparent = false:silent` to `csharp_style_var_when_type_is_apparent = true:suggestion`.

Leave everything else (naming rules, spacing, `insert_final_newline = false`) at the SDK default — CLAUDE.md doesn't specify those.

- [ ] **Step 10: Format once to normalize template-generated files, then verify clean**

```powershell
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
& $dotnet format Transom.sln
& $dotnet format Transom.sln --verify-no-changes
```

Expected: the first call reports fixes (whitespace/final-newline in the template's `.cs` files); the second call exits 0 with no output.

- [ ] **Step 11: Build the whole solution with the documented command**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" build Transom.sln -c Debug -p:Platform=x64
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 12: Run the (currently empty) test project**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test tests\Transom.Core.Tests
```

Expected: `A total of 0 test files matched the specified pattern` is NOT what we want — since `UnitTest1.cs` was deleted in Step 3, the project will report zero tests found, which is fine for this task (Task 2 adds the first real test). Confirm it builds and runs without error (exit code 0, "Passed! ... Total: 0" or equivalent "no test source files" message — either is acceptable here since Task 2 adds the first test).

- [ ] **Step 13: Commit**

```bash
git add Transom.sln Directory.Build.props .editorconfig src tests
git commit -m "$(cat <<'EOF'
chore: scaffold Transom solution (Core, App, Tests) (#1)

WinUI NavigationView shell (Mica, empty pane), Directory.Build.props
scoping nullable/warnings-as-errors, editorconfig matching CLAUDE.md style.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: Authorization-redacting HTTP logging handler (Core, TDD)

**Files:**
- Create: `src/Transom.Core/Http/RedactingLoggingHandler.cs`
- Create: `tests/Transom.Core.Tests/Http/RedactingLoggingHandlerTests.cs`
- Modify: `src/Transom.Core/Transom.Core.csproj` (add `Microsoft.Extensions.Logging.Abstractions` package reference)

**Interfaces:**
- Consumes: nothing from Task 1 beyond the empty `Transom.Core`/`Transom.Core.Tests` projects.
- Produces: `Transom.Core.Http.RedactingLoggingHandler`, a `public sealed class RedactingLoggingHandler : DelegatingHandler` with constructor `RedactingLoggingHandler(ILogger<RedactingLoggingHandler> logger)`. Task 3 registers this in `Transom.App`'s DI container and attaches it to a named `HttpClient` via `AddHttpMessageHandler<RedactingLoggingHandler>()`.

- [ ] **Step 1: Add the logging abstractions package to Core**

Edit `src/Transom.Core/Transom.Core.csproj`, adding inside a new `<ItemGroup>`:

```xml
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
```

- [ ] **Step 2: Write the failing test**

Write `tests/Transom.Core.Tests/Http/RedactingLoggingHandlerTests.cs`:

```csharp
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
```

- [ ] **Step 3: Run to verify it fails**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test tests\Transom.Core.Tests --filter RedactingLoggingHandlerTests
```

Expected: FAIL — `Transom.Core.Http` / `RedactingLoggingHandler` does not exist (CS0246).

- [ ] **Step 4: Write the implementation**

Write `src/Transom.Core/Http/RedactingLoggingHandler.cs`:

```csharp
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace Transom.Core.Http;

/// <summary>
/// A <see cref="DelegatingHandler"/> that logs outgoing requests and incoming status codes
/// without ever writing an <c>Authorization</c> header value to the log.
/// </summary>
public sealed class RedactingLoggingHandler : DelegatingHandler
{
    private readonly ILogger<RedactingLoggingHandler> _logger;

    public RedactingLoggingHandler(ILogger<RedactingLoggingHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("{Method} {Uri} Authorization: {Authorization}", request.Method, request.RequestUri, RedactAuthorization(request.Headers.Authorization));
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("{Method} {Uri} -> {StatusCode}", request.Method, request.RequestUri, (int)response.StatusCode);
        return response;
    }

    internal static string RedactAuthorization(AuthenticationHeaderValue? authorization)
    {
        return authorization is null ? "(none)" : $"{authorization.Scheme} [REDACTED]";
    }
}
```

- [ ] **Step 5: Run to verify it passes**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test tests\Transom.Core.Tests --filter RedactingLoggingHandlerTests
```

Expected: `Passed! ... Total: 2`.

- [ ] **Step 6: Run the full solution build + format check**

```powershell
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
& $dotnet build Transom.sln -c Debug -p:Platform=x64
& $dotnet format Transom.sln --verify-no-changes
```

Expected: both clean.

- [ ] **Step 7: Commit**

```bash
git add src/Transom.Core/Http src/Transom.Core/Transom.Core.csproj tests/Transom.Core.Tests/Http
git commit -m "$(cat <<'EOF'
feat: add Authorization-redacting HTTP logging handler (#1)

RedactingLoggingHandler strips Authorization header values before
they reach any log sink; verified with a fake inner handler and an
obviously-fake test token.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: DI host wiring in Transom.App

**Files:**
- Create: `src/Transom.App/HostBuilderExtensions.cs`
- Modify: `src/Transom.App/App.xaml.cs`
- Modify: `src/Transom.App/Transom.App.csproj` (add `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Http` package references, add `ProjectReference` to `Transom.Core`)

**Interfaces:**
- Consumes: `Transom.Core.Http.RedactingLoggingHandler` from Task 2 (constructor `RedactingLoggingHandler(ILogger<RedactingLoggingHandler> logger)`).
- Produces: a `Microsoft.Extensions.Hosting.IHost` accessible as `App.Host` (static property) for later view models/services to resolve dependencies from (used starting M1).

- [ ] **Step 1: Add package references and the Core project reference**

Edit `src/Transom.App/Transom.App.csproj`, adding inside a new `<ItemGroup>`:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.0" />
  <PackageReference Include="Microsoft.Extensions.Http" Version="10.0.0" />
</ItemGroup>

<ItemGroup>
  <ProjectReference Include="..\Transom.Core\Transom.Core.csproj" />
</ItemGroup>
```

- [ ] **Step 2: Write `HostBuilderExtensions`**

Write `src/Transom.App/HostBuilderExtensions.cs`:

```csharp
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
```

- [ ] **Step 3: Wire the host into `App.xaml.cs`**

Read the current `src/Transom.App/App.xaml.cs` first (its exact contents depend on what the `winui-navview` template generated in Task 1 for the `Transom.App` namespace), then modify it to build and start the host before creating `MainWindow`, and stop it on exit. The result should match this shape:

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;

namespace Transom.App;

public partial class App : Application
{
    private Window? _window;

    public static IHost Host { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureTransomServices()
            .Build();

        _window = new MainWindow();
        _window.Activate();
    }
}
```

(Keep any XML doc comments the template already has on `App`/`OnLaunched`; only the body changes.)

- [ ] **Step 4: Build**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" build Transom.sln -c Debug -p:Platform=x64
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 5: Format check**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" format Transom.sln --verify-no-changes
```

Expected: clean.

- [ ] **Step 6: Commit**

```bash
git add src/Transom.App
git commit -m "$(cat <<'EOF'
feat: wire Generic Host and HttpClientFactory into Transom.App (#1)

App.xaml.cs builds and starts an IHost on launch; a named MicroBlog
HttpClient is registered with RedactingLoggingHandler in its pipeline
so future Micropub/JSON API clients get Authorization redaction for
free.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: GitHub Actions CI

**Files:**
- Create: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: `Transom.sln`, `src/Transom.Core/Transom.Core.csproj`, `tests/Transom.Core.Tests/Transom.Core.Tests.csproj` (all from Task 1).
- Produces: nothing consumed by later tasks — this is the terminal CI task for M0.

- [ ] **Step 1: Write the workflow**

Write `.github/workflows/ci.yml`:

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:

jobs:
  build-windows:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore
        run: dotnet restore Transom.sln

      - name: Build
        run: dotnet build Transom.sln -c Debug -p:Platform=x64 --no-restore

      - name: Test
        run: dotnet test tests/Transom.Core.Tests --no-restore

      - name: Format check
        run: dotnet format Transom.sln --verify-no-changes

  core-linux:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore
        run: dotnet restore src/Transom.Core/Transom.Core.csproj tests/Transom.Core.Tests/Transom.Core.Tests.csproj

      - name: Build
        run: dotnet build src/Transom.Core/Transom.Core.csproj tests/Transom.Core.Tests/Transom.Core.Tests.csproj --no-restore

      - name: Test
        run: dotnet test tests/Transom.Core.Tests --no-restore
```

Note: `core-linux` restores/builds/tests only `Transom.Core` and `Transom.Core.Tests` explicitly (not the `.sln`), since `Transom.App` targets `net10.0-windows10.0.26100.0` with `UseWinUI=true` and cannot restore on Linux.

- [ ] **Step 2: Validate YAML syntax locally**

```powershell
& "C:\Program Files\dotnet\dotnet.exe" tool run --version 2>&1 | Out-Null  # no-op sanity check that dotnet tool invocation works
python -c "import yaml, sys; yaml.safe_load(open('.github/workflows/ci.yml')); print('OK')"
```

If `python`/`pyyaml` isn't available, visually re-check indentation instead — there is no GitHub Actions CLI validator available offline in this environment; the workflow's correctness will be confirmed the first time it runs on a pushed branch/PR, which is out of scope to trigger from this session.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "$(cat <<'EOF'
ci: add GitHub Actions build/test workflow (#1)

windows-latest builds+tests+format-checks the full solution;
ubuntu-latest builds+tests Transom.Core and Transom.Core.Tests only,
since Transom.App requires the Windows App SDK.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: Update milestones

**Files:**
- Modify: `docs/milestones.md`

**Interfaces:**
- Consumes: nothing.
- Produces: nothing.

- [ ] **Step 1: Tick the delivered M0 boxes**

Edit `docs/milestones.md`, changing under `## M0 — Scaffold and CI`:

```markdown
- [x] Solution with `Transom.Core`, `Transom.App` (WinUI 3 packaged), `Transom.Core.Tests`
- [ ] DI host, logging with Authorization redaction, `TransomJsonContext`
- [x] `.editorconfig`, nullable on, warnings-as-errors in Core
- [x] GitHub Actions: build + test on `windows-latest`, Core tests on `ubuntu-latest`
- [x] Empty `NavigationView` shell with Mica, light/dark
```

Leave the DI/logging/TransomJsonContext line unchecked and add a note directly under the M0 heading:

```markdown
> DI host and Authorization-redacting logging are done; `TransomJsonContext` is
> deferred to M1, created alongside the first real response DTO — an empty
> `JsonSerializerContext` with no `[JsonSerializable]` types does not compile
> (source generator requires at least one).
```

- [ ] **Step 2: Commit**

```bash
git add docs/milestones.md
git commit -m "$(cat <<'EOF'
docs: tick M0 scaffold/CI boxes, note TransomJsonContext deferred to M1 (#1)

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Self-review

1. **Spec coverage** — M0 checklist: solution/projects ✅ (Task 1), DI host ✅ / logging+redaction ✅ (Tasks 2–3) / `TransomJsonContext` explicitly and reasonedly deferred (documented, not silently dropped), `.editorconfig`+nullable+warnings-as-errors ✅ (Task 1), GitHub Actions both OSes ✅ (Task 4), empty NavigationView shell with Mica + light/dark ✅ (Task 1 — light/dark is automatic via WinUI's default `RequestedTheme` following system, no extra code needed).
2. **Placeholder scan** — no TBD/TODO; the one deliberately-not-implemented item (`TransomJsonContext`) is explained with a verified technical reason and an explicit decision, not left vague.
3. **Type consistency** — `RedactingLoggingHandler(ILogger<RedactingLoggingHandler> logger)` used identically in Task 2's test and Task 3's DI registration; `RedactingLoggingHandler` namespace `Transom.Core.Http` consistent across both tasks.
4. Every shell command in every step was executed against a throwaway probe scaffold this session and its actual output is what's transcribed above — no invented command output.
