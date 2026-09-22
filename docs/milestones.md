# Milestones

Each milestone is shippable and sized for a few focused Claude Code sessions. Each checkbox
should become one GitHub issue and one PR. Story IDs (U#) refer to `SPEC.md` §3.

## M0 — Scaffold and CI
- [x] Solution with `Transom.Core`, `Transom.App` (WinUI 3 packaged), `Transom.Core.Tests`
- [x] DI host, logging with Authorization redaction
- [x] `.editorconfig`, nullable on, warnings-as-errors in Core
- [x] GitHub Actions: build + test on `windows-latest`, Core tests on `ubuntu-latest`
- [x] Empty `NavigationView` shell with Mica, light/dark
**Done when:** CI is green and the app opens to an empty shell.

## M1 — Sign in and text post (U1, U2)
- [ ] `TransomJsonContext` (source-generated) with the first real DTOs
- [ ] `ICredentialStore` + `PasswordVault` implementation
- [ ] Settings → paste app token → verify (`/account/verify` or `q=config`) → show avatar/name
- [ ] `MicropubClient.GetConfigAsync`, `PublishAsync` (text + optional title)
- [ ] Composer: text, character count, auto Title field over 300 chars, Ctrl+Enter publishes
- [ ] Success InfoBar with link to the post; failure keeps text and offers Retry
**Done when:** I can publish a text post from the app and see it on my blog.

## M2 — Images (U3)
- [ ] Media endpoint upload with progress (`multipart/form-data`, part `file`)
- [ ] App `IImageProcessor`: resize to 2048 px, JPEG q85, strip metadata; GIF passthrough
- [ ] Composer image tray: picker, drag-drop, clipboard paste, reorder, remove
- [ ] Alt text per image; `photo[]` + `mp-photo-alt[]` in publish
**Done when:** a post with 3 images and alt text appears correctly on the blog.

## M3 — Timeline (U4)
- [ ] JSON Feed + `_microblog` models and `TimelineClient` (timeline, paging with `before_id`/`since_id`)
- [ ] Spike: `HtmlToXamlConverter` vs WebView2 — record decision in SPEC §12
- [ ] Virtualized timeline list, avatars cached, relative times, image thumbnails + viewer
- [ ] Refresh + 2-minute poll with "N new posts" pill; keep scroll position
- [ ] Open in browser / copy link
**Done when:** I can read my timeline smoothly and scroll back a few hundred posts.

## M4 — Conversations, replies, mentions, bookmarks (U5, U6)
- [ ] Conversation view (`/posts/conversation`)
- [ ] Reply (`/posts/reply`) from timeline and conversation, pre-filled `@username`
- [ ] Mentions, Discover, Bookmarks feeds; bookmark toggle
- [ ] User profile view (`/posts/<username>`), follow/unfollow
**Done when:** I can hold a conversation entirely inside the app.

## M5 — Drafts, categories, multiple blogs (U7, U8)
- [ ] Local draft store with 3-second autosave, Drafts page, resume/delete
- [ ] Server drafts: publish with `post-status=draft`, list via `q=source`
- [ ] Categories picker (`q=category`)
- [ ] Blog picker from `destination[]`; `mp-destination` on posts and uploads
**Done when:** a half-written post survives closing the app, and I can post to my second blog.

## M6 — Tray, hotkey, quick post (U9)
- [ ] Tray icon (open, quick post, quit), minimize to tray, start with Windows
- [ ] Global hotkey (default Ctrl+Alt+M, configurable)
- [ ] Quick-post window (always on top, Esc hides and keeps the draft)
**Done when:** I can post from anywhere in Windows without opening the main window.

## M7 — v1.0 release
- [ ] IndieAuth sign-in (PKCE, loopback redirect, system browser) (U10)
- [ ] Accessibility pass (Accessibility Insights), keyboard shortcut sheet (F1)
- [ ] Release workflow: signed MSIX + `.appinstaller` on tag `v*`
- [ ] README screenshots, install guide, CHANGELOG
**Done when:** v1.0.0 is on GitHub Releases and installs cleanly on a fresh PC.

## Later
- Edit/delete posts (Micropub update/delete)
- Toast notifications for mentions
- Providers: generic Micropub, Mastodon, Bluesky (see SPEC §9)
- winget / Microsoft Store listing
