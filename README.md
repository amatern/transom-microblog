# Transom

A native Windows client for [Micro.blog](https://micro.blog): read your timeline, reply, and
publish short posts with images. Built with WinUI 3 and .NET.

> **Status:** early development, not usable yet. See [`docs/milestones.md`](docs/milestones.md).

## Planned features
- Timeline, mentions, discover, bookmarks, conversations and replies
- Compose short and long posts in Markdown, with a live preview
- Drag, paste or pick images, with alt text and automatic resizing
- Drafts, categories, and multiple blogs
- Tray icon and a global hotkey for quick posts
- Designed so other platforms (Mastodon, Bluesky, other Micropub blogs) can be added later

## Built with Claude
This project is co-written with [Claude](https://claude.ai). The spec (`SPEC.md`) and the
working agreement for AI-assisted development (`CLAUDE.md`) are part of the repo so you can
see how it's built. Commits with AI help carry a `Co-Authored-By` trailer.

## Building
```powershell
git clone https://github.com/amatern/transom-microblog.git
cd transom-microblog
```

Requires Windows 10 1809+ / Windows 11, the .NET 10 SDK and Visual Studio 2022+ with the
"Windows application development" workload.

```powershell
dotnet build Transom.sln -c Debug -p:Platform=x64
dotnet test tests/Transom.Core.Tests
```

## Contributing
Issues and PRs are welcome. Please read `SPEC.md` first and keep PRs to one milestone task.

## License
MIT — see `LICENSE`.

Transom is an independent project and is not affiliated with Micro.blog.
