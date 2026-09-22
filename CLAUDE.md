# CLAUDE.md — working agreement for Claude Code

Transom is a native Windows client for Micro.blog. Read `SPEC.md` before any feature work
and `docs/milestones.md` to see what's in scope right now. Only build what the current
milestone asks for.

## Stack
- .NET 10, C#, WinUI 3 (Windows App SDK, packaged MSIX), MVVM with CommunityToolkit.Mvvm.
- DI via Microsoft.Extensions.Hosting; HTTP via `IHttpClientFactory`.
- `System.Text.Json` source generation (`TransomJsonContext`). No Newtonsoft.
- Tests: xUnit, fake `HttpMessageHandler`, JSON fixtures in `tests/Transom.Core.Tests/Fixtures/`.

## Layout
- `src/Transom.Core` — protocol, models, providers, drafts. **Must not reference WinUI or
  any `Windows.*` API.** It builds and tests on Linux.
- `src/Transom.App` — WinUI views, view models, platform services.
- `tests/Transom.Core.Tests`

## Commands
```powershell
dotnet restore Transom.sln -p:Platform=x64
dotnet build Transom.sln -c Debug -p:Platform=x64
dotnet test tests/Transom.Core.Tests
dotnet format --verify-no-changes
```
Restore and build must use the same `-p:Platform`. A restore without it produces assets that
the x64 WinUI build can't use (NETSDK1047).

Run the app from Visual Studio 2022+ (Transom.App (Package) profile) or
`dotnet run --project src/Transom.App -p:Platform=x64`.

## Rules
1. **Never log, print, persist or commit tokens.** Tokens live in `PasswordVault` only.
   The logging handler redacts `Authorization`. Test tokens are obviously fake (`test-token`).
2. No real network calls in tests. Record fixtures by hand from API docs; strip personal data.
3. Provider-specific code stays in `Core/Providers/<Name>/`. UI talks to `IBlogProvider` /
   `ITimelineProvider` and checks `Capabilities`/`Limits` — never `if (provider is MicroBlog…)`.
4. Every async method takes a `CancellationToken`. No `.Result` / `.Wait()`.
5. View models have no WinUI types; they are unit-testable. Use `[ObservableProperty]` and
   `[RelayCommand]`.
6. The composer must never lose user text: autosave drafts before any network call.
7. Accessibility: every interactive control gets `AutomationProperties.Name`; keyboard works
   everywhere.
8. Nullable reference types on, warnings as errors in Core.
9. Keep PRs small — one milestone task per PR, with tests. Update `SPEC.md` if a decision
   changes, and tick the box in `docs/milestones.md`.

## Style
- File-scoped namespaces, `var` when the type is obvious, records for DTOs/models.
- XAML: use `x:Bind`, theme resources (no hard-coded colors), Fluent icons (`SymbolIcon`/`FontIcon`).
- Public APIs in Core get XML doc comments.

## Commits
Conventional commits (`feat:`, `fix:`, `docs:`, `test:`, `chore:`), referencing the issue
(`feat: timeline paging (#12)`).
