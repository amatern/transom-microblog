# Transom — Specification

> A native Windows client for [Micro.blog](https://micro.blog), built with WinUI 3.
> Repository: `transom-microblog` on GitHub. Co-written with Claude. Status: **draft v0.1** (2026-09-21).

## 1. Why

Micro.blog has good clients on iOS, Android and macOS, but no dedicated Windows client
([third-party apps list](https://help.micro.blog/t/third-party-apps/46)). Windows users post
through the web UI. Transom is a fast, keyboard-friendly, native-feeling app for **reading the
timeline and publishing short posts with images**, designed so other blogging platforms can be
added later.

## 2. Goals and non-goals

### Goals (v1.0)
- Read the Micro.blog timeline, mentions, discover and bookmarks; open conversations; reply.
- Compose and publish posts (short and long/titled), with Markdown.
- Upload images (drag-drop, paste, file picker) with alt text.
- Drafts: local autosave and Micro.blog server-side drafts.
- Multiple Micro.blog blogs per account (`mp-destination`).
- Quick-post window from tray icon and a global hotkey.
- Feels native: Fluent design, Mica, light/dark, keyboard shortcuts, accessible.
- A provider abstraction so other platforms can be added without rewriting the UI.

### Non-goals (v1.0)
- Editing blog themes, pages, or Micro.blog account settings.
- Podcasts, video upload, newsletters, book shelves.
- Direct cross-posting to Mastodon/Bluesky (Micro.blog already cross-posts; see §9).
- macOS/Linux builds.

## 3. Users and core stories

Primary user: someone with a Micro.blog account who writes short posts and reads the timeline
on a Windows PC.

| # | Story | Milestone |
|---|---|---|
| U1 | I paste an app token once and stay signed in securely. | M1 |
| U2 | I write a short post and publish it in under 5 seconds. | M1 |
| U3 | I drag or paste an image in, add alt text, and publish. | M2 |
| U4 | I scroll my timeline, newest first, and new posts appear without losing my place. | M3 |
| U5 | I open a conversation, read the thread, and reply. | M4 |
| U6 | I see my mentions and bookmark posts to read later. | M4 |
| U7 | I start a long post, close the app, and find my draft when I return. | M5 |
| U8 | I choose which of my blogs to post to. | M5 |
| U9 | I press a hotkey anywhere in Windows, type a thought, hit Ctrl+Enter, done. | M6 |
| U10 | I sign in through my browser instead of pasting a token. | M7 |

## 4. UX

### 4.1 Main window
`NavigationView` (left rail) with sections:
- **Timeline** (default), **Mentions**, **Discover**, **Bookmarks**, **My posts**, **Drafts**
- Footer: account/blog switcher, **Settings**
- A persistent **New post** button (Ctrl+N) opens the composer.

Timeline item layout: avatar, display name, @username, relative time (tooltip = absolute), post
body, image thumbnails (click → full-size viewer), actions: **Reply**, **Conversation**,
**Bookmark**, **Open in browser**, **Copy link**.

Behaviours:
- Infinite scroll downward (`before_id`); pull/refresh button and auto-poll every 2 min
  (`since_id`). New items appear behind a "N new posts" pill; scroll position is preserved.
- Clicking @mentions opens that user's posts in-app; links open in the default browser.
- Unread position remembered per feed across restarts.

### 4.2 Composer
- Multi-line text box, Markdown supported. Live character count; **Title** field appears
  automatically when the text exceeds 300 characters (Micro.blog's short/long post threshold)
  and can be shown manually.
- Optional Markdown **Preview** toggle (Markdig → rendered).
- Image tray under the text: thumbnails, per-image **alt text** (prompted on add; warning if
  empty), remove, reorder.
- Category picker (loaded from the blog), blog picker (if >1), **Save draft**,
  **Publish** (Ctrl+Enter).
- Upload progress per image; posting is blocked until uploads complete.
- On failure: text is never lost; show error InfoBar with **Retry**.

### 4.3 Quick-post window
Small, always-on-top window (text + image + Publish). Opened from tray icon or global hotkey
(default Ctrl+Alt+M, configurable). Esc hides it, keeping contents as a local draft.

### 4.4 Settings
Account(s) and sign-out, default blog, hotkey, image settings (max dimension, JPEG quality,
strip metadata — on by default), poll interval, theme, start with Windows, minimize to tray.

### 4.5 Accessibility and polish
All actions keyboard-reachable; `AutomationProperties.Name` on every control; high-contrast
works; respects system text scaling; Mica backdrop; light/dark follows system.

## 5. Architecture

```
Transom.sln
├─ src/Transom.Core        net10.0 class library — NO WinUI references
│   ├─ Providers/           IBlogProvider, ITimelineProvider, capabilities
│   ├─ MicroBlog/           Micropub + JSON API clients, DTOs
│   ├─ Media/               IImageProcessor abstraction
│   ├─ Drafts/              local draft store (JSON files)
│   └─ Models/              Post, TimelineItem, Author, MediaItem, Draft, BlogInfo
├─ src/Transom.App         WinUI 3 app (Windows App SDK, packaged MSIX)
│   ├─ Views/ ViewModels/   MVVM (CommunityToolkit.Mvvm)
│   ├─ Services/            credentials, image processing, tray, hotkey, notifications
│   └─ Rendering/           HTML → RichTextBlock converter
└─ tests/Transom.Core.Tests  xUnit; HTTP mocked
```

Key choice: all network/protocol logic lives in `Transom.Core` so it is testable on any OS
(including Linux CI) and reusable if a CLI or other front-end is ever added.

### 5.1 Stack
- .NET 10 (LTS), C#, Windows App SDK (latest stable), WinUI 3, packaged (MSIX).
- CommunityToolkit.Mvvm; Microsoft.Extensions.DependencyInjection/Hosting/Http/Logging.
- `System.Text.Json` with source generation.
- Markdig (Markdown preview), HtmlAgilityPack (HTML → XAML rendering).
- Images: `Windows.Graphics.Imaging` (resize/re-encode, which drops EXIF) in the App layer.
- Credentials: `Windows.Security.Credentials.PasswordVault`.
- Tests: xUnit + a fake `HttpMessageHandler`.

### 5.2 Provider abstraction

```csharp
public interface IBlogProvider
{
    string Id { get; }                     // "microblog", later "mastodon", "bluesky"
    ProviderCapabilities Capabilities { get; }
    Task<AccountInfo> VerifyAsync(CancellationToken ct);
    Task<IReadOnlyList<BlogInfo>> GetBlogsAsync(CancellationToken ct);
    Task<IReadOnlyList<string>> GetCategoriesAsync(BlogInfo blog, CancellationToken ct);
    Task<MediaItem> UploadMediaAsync(Stream data, string fileName, string contentType,
                                     IProgress<double>? progress, CancellationToken ct);
    Task<PublishResult> PublishAsync(PostDraft draft, CancellationToken ct);
    Task<IReadOnlyList<RemoteDraft>> GetRemoteDraftsAsync(BlogInfo blog, CancellationToken ct);
}

public interface ITimelineProvider
{
    Task<TimelinePage> GetFeedAsync(FeedKind kind, PageRequest page, CancellationToken ct);
    Task<IReadOnlyList<TimelineItem>> GetConversationAsync(string postId, CancellationToken ct);
    Task ReplyAsync(string postId, string content, CancellationToken ct);
    Task SetBookmarkAsync(string postId, bool bookmarked, CancellationToken ct);
}

[Flags] public enum ProviderCapabilities
{ None = 0, Titles = 1, Categories = 2, RemoteDrafts = 4, MultipleBlogs = 8,
  AltText = 16, Markdown = 32, Timeline = 64 }

public record ProviderLimits(int? MaxChars, int? MaxImages, long? MaxImageBytes);
```

The UI reads `Capabilities`/`Limits` to show or hide features (e.g. no Title field for
Bluesky, character counter turns red at 300 for Bluesky, 500 for Mastodon).

## 6. Micro.blog API reference (what v1 uses)

Base URL `https://micro.blog`. All requests: `Authorization: Bearer <token>`.

### 6.1 Auth
- **v1:** user creates an app token at micro.blog → Account → **App tokens** and pastes it.
- Verify token and fetch profile: `POST /account/verify` (form: `token=<token>`) → name,
  username, avatar. *(Confirm exact response shape in M1; fall back to `GET /micropub?q=config`
  as the validity check.)*
- **M7:** IndieAuth sign-in with PKCE and a loopback redirect (`http://127.0.0.1:<port>/`),
  system browser, no embedded web view.

### 6.2 Posting (Micropub) — [docs](https://help.micro.blog/t/posting-api/96)
| Purpose | Request |
|---|---|
| Config, blogs, media endpoint | `GET /micropub?q=config` → `media-endpoint`, `destination[]` |
| Categories | `GET /micropub?q=category` *(standard Micropub; confirm in M5)* |
| Create post | `POST /micropub` form-encoded: `h=entry&content=...` |
| Title | `name=...` |
| Category | `category[]=...` (repeatable) |
| Image | `photo[]=<url>` + `mp-photo-alt[]=<alt>` (same order) |
| Draft | `post-status=draft` → response includes `preview` URL |
| Target blog | `mp-destination=<blog uid>` |
| List posts/drafts | `GET /micropub?q=source` (+ `mp-destination`, filter `post-status`) |
| Update / delete | JSON `{"action":"update"|"delete","url":...}` (W3C Micropub) — v1.1 |

Success: `201 Created` / `202 Accepted` with `Location` header = post URL.

**Media upload:** `POST <media-endpoint>` (`https://micro.blog/micropub/media`),
`multipart/form-data`, part name `file` → `202 Accepted`, `Location: https://…/uploads/…jpg`.
Pass `mp-destination` when the user has multiple blogs.

### 6.3 Reading (JSON API) — [docs](https://help.micro.blog/t/json-api/97)
Responses are [JSON Feed](https://jsonfeed.org) with `_microblog` extension fields.

| Feed | Request |
|---|---|
| Timeline | `GET /posts/timeline` |
| Mentions | `GET /posts/mentions` |
| Discover | `GET /posts/discover` (and `/posts/discover/<collection>`) |
| Photos | `GET /posts/photos` |
| Bookmarks | `GET /posts/bookmarks`; add `POST /posts/bookmarks` (`id`); remove `DELETE /posts/bookmarks/<id>` |
| User's posts | `GET /posts/<username>` |
| Conversation | `GET /posts/conversation?id=<id>` |
| Reply | `POST /posts/reply` (`id`, `content`) |
| Follow | `POST /users/follow` / `POST /users/unfollow` (`username`), `GET /users/is_following?username=` |

Pagination: `count`, `before_id` (older), `since_id` (newer).

Rendering: `content_html` is rendered by `Rendering/HtmlToXamlConverter` into a
`RichTextBlock`, supporting `p, br, a, strong/b, em/i, blockquote, ul/ol/li, code, img`
(images become thumbnails below the text). Unknown tags degrade to their text. No scripts,
no iframes. Items too complex to render get an **Open in browser** fallback.

### 6.4 Rate limits and etiquette
Poll no more often than every 60 s; back off exponentially on 429/5xx; send a
`User-Agent: Transom/<version> (+https://github.com/amatern/transom-microblog)`.

## 7. Images
- Sources: file picker (multi-select), drag-drop, clipboard paste (bitmap or file).
- Types: JPEG, PNG, GIF (animated GIF uploaded untouched), WebP, HEIC (if codec present).
- Processing (on by default, configurable): resize so the long edge ≤ 2048 px, re-encode JPEG
  q=85, which strips EXIF/GPS. PNG stays PNG unless > 5 MB.
- Up to 10 images per post (Micro.blog shows multiple photos as a gallery).
- Alt text stored with the draft; empty alt text shows a gentle warning, never blocks.

## 8. Storage, security and privacy
- Tokens: `PasswordVault` only (resource `Transom`, user = account id). Never in settings
  files, logs, crash reports or exceptions.
- Settings: `ApplicationData.Current.LocalSettings`.
- Drafts: JSON in `LocalFolder/drafts/`, autosaved every 3 s while typing; images copied into
  the draft folder until published.
- Cache: avatars and thumbnails in `LocalCacheFolder`, capped at 200 MB (LRU).
- Logging: `Microsoft.Extensions.Logging` to a rolling file in `LocalFolder/logs`, with the
  `Authorization` header redacted. No telemetry.

## 9. Other platforms (post-v1)
Micro.blog already cross-posts to Mastodon, Bluesky, Threads, LinkedIn, Tumblr and others, so
direct posting is only worth it for people who don't route everything through Micro.blog.

| Provider | Effort | Notes |
|---|---|---|
| Generic Micropub (WordPress + IndieWeb plugin, others) | Low | Reuse Micropub client; IndieAuth discovery needed |
| Mastodon | Low–Med | OAuth app registration per instance; `/api/v2/media`; 500 chars |
| Bluesky | Medium | AT Protocol; 300 graphemes; link/mention facets; ~1 MB image blobs; app password or OAuth |
| WordPress.com / Ghost | Medium | Different auth; posts are HTML/Lexical |

Rule for the codebase: **nothing outside `Providers/<name>/` may know which provider is active**,
other than through `Capabilities` and `Limits`.

## 10. Quality bar
- Cold start to usable timeline < 2 s on a typical machine (cached first page shown instantly).
- Timeline scrolling stays smooth with 1,000 items (virtualized `ItemsRepeater`/`ListView`).
- No data loss: composer text survives crashes, network failures and app closes.
- Core library ≥ 80% line coverage; every API call has a test using recorded JSON fixtures.
- Accessibility Insights FastPass passes on main window and composer.

## 11. Distribution
- GitHub Releases: MSIX + `.appinstaller`, self-signed at first (install instructions in README),
  later a trusted cert (e.g. Azure Trusted Signing) and **winget**; Microsoft Store is optional.
- GitHub Actions: build + test on every PR (`windows-latest`); Core tests also on
  `ubuntu-latest`; release workflow on tags `v*`.

## 12. Open questions
1. ~~Final app name~~ — decided 2026-09-21: **Transom**, repo `transom-microblog`. Store listing: "Transom – a Micro.blog client".
2. HTML rendering: custom RichTextBlock converter (native feel, planned) vs. one WebView2 per
   timeline (full fidelity, heavier). Revisit after M3 spike.
3. Should replies support images? (JSON API reply is text-only; could post via Micropub
   `in-reply-to` instead.)
4. Toast notifications for new mentions — v1 or later?
5. Edit/delete existing posts — v1.1?
