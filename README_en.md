<div align="center">

# <img width="32" height="32" alt="icon" src="https://github.com/user-attachments/assets/dfae4206-78de-45de-a975-d9b69b96c68b" /> IwaraDownloader

**Feature-rich video downloader & media server for iwara.tv / iwara.ai on Windows**

[![Version](https://img.shields.io/badge/version-2.5.1-blue.svg)](https://github.com/dekotan24/iwara-downloader/releases)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6.svg)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

Subscribe to channels → auto-download new videos → watch from any browser on your LAN.<br>
A Windows desktop app that handles everything from downloading to collection management.

**[日本語 README](README.md)**

[Features](#-features) • [Installation](#-installation) • [Usage](#-usage) • [Web Media Server](#-web-media-server) • [Troubleshooting](#-troubleshooting)

</div>

---

<img width="1000" height="652" alt="2026-06-12_14h30_40" src="https://github.com/user-attachments/assets/de36894c-65e9-41af-b150-e65c7ff4a8bf" />

## ✨ Highlights

- 📺 **Channel subscriptions** — register your favorite users and new uploads are detected and downloaded automatically
- 🔄 **Robust downloads** — resume (HTTP Range + integrity checks), automatic retries, auto-resume on startup, automatic queue suspend/resume on login expiry
- 🌐 **Built-in web media server** — watch and manage your library from a phone or tablet browser on your LAN
- ⭐ **Favorites & powerful search** — manage favorites from both the app and the web UI, mixed title/artist/tag search
- 🧰 **Collection management** — UUID-based duplicate detection, crash-safe library relocation, statistics dashboard
- 🗑️ **Trash / exclusion list** — deleted videos never come back via auto-fetch, and can be restored anytime
- 🌐 **Multi-language UI** — Japanese / English / Simplified Chinese (switchable anytime in Settings)
- 🔒 **Fully local** — all data stays on your machine; nothing is ever sent anywhere

## 📋 Features

### 📥 Downloading

| Feature | Description |
|---------|-------------|
| Channel subscriptions | Just add a username / profile URL. Per-channel save folders |
| Automatic new-video checks | Periodic polling with a serialized worker (timeouts, exponential backoff, priority queue) |
| Single downloads | Paste a video URL and hit Enter. With clipboard monitoring enabled, copying a URL is enough |
| Bulk import | Import from URL lists, iwara search results, or a local folder. mp4 files without iwara tags (mp4 only, no m4v support) can also be matched against DB videos by filename/folder structure and recognized as already downloaded |
| Resume | Continues after disconnects / pauses via HTTP Range (verified by file_id / size / ETag) |
| Parallel downloads | Up to 3 concurrent downloads + configurable rate limits (API interval, download interval, 429 backoff) |
| iwara.ai support | Site is detected automatically from the URL |

### ⭐ Organizing & Search

- **Favorites** — toggle via right-click, the ★ on thumbnails, or the web UI. One click on the "⭐ Favorites" tree node lists them all
- **Mixed search** — one search box covers title / artist / tags / memo
- **Search syntax** — field filters and exclusions such as `tag:vr`, `author:foo`, `status:failed`, `fav:true`, `-excluded`, `"quoted phrase"`
- **NSFW filter** — switch between All / SFW / NSFW
- **Thumbnail view** — toggle between detail list and tile view; thumbnails are fetched and cached automatically

### 📁 File & Collection Management

- **UUID tagging** — embeds the iwara UUID into each mp4, preventing duplicate downloads even if filenames or the DB change
- **Save-folder relocation** — free-space check → progress dialog. A journal makes moves crash-safe and auto-recovered on restart
- **Bulk move of leftover files** — files that failed to move (e.g. out of disk space) can be re-moved in one click later
- **Relink externally moved files** — after moving files with FastCopy etc., verifies them (size → UUID) and updates only the DB paths
- **Trash / exclusion list** — deleting a video moves it to the exclusion list so channel auto-checks won't re-download it. Restore or permanently delete it from the "🚫 Excluded" tree node
- **Duplicate check / bulk rename / statistics dashboard** (video counts, success rate, daily trends)

### 🌐 Web Media Server

Start the built-in web server and **watch / manage your library from any browser on your LAN**.

- 🎞️ Streaming playback with seeking, playlists, continuous play, shuffle, keyboard shortcuts
- 🔍 Search (space-separated AND), sorting, grid / list views, per-channel browsing
- ⭐ Add / remove favorites, favorites view
- 🚦 Retry from the error list, download queue status, statistics
- 🔐 Username + password auth (password stored encrypted via DPAPI, 24-hour sessions)

> ⚠️ Designed for LAN use. Do not expose it directly to the internet.

## 🖥️ Requirements

| Item | Requirement |
|------|-------------|
| OS | Windows 10 / 11 (64-bit) |
| Runtime | [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Python | Not required (the first-run setup wizard fetches it automatically). An existing Python 3.10+ can also be used |
| Display language | Japanese / English / Simplified Chinese (switch in Settings → General; "Auto" follows the OS language) |

## 🚀 Installation

1. **Download** — grab the latest release from [Releases](https://github.com/dekotan24/iwara-downloader/releases) and extract it anywhere
2. **Launch** — run `IwaraDownloader.exe`; the setup wizard opens automatically on first run
3. **Follow the wizard** — it fetches Python and installs the required packages automatically (you can also point it at an existing Python installation)
4. **Login** — sign in with your iwara.tv email and password, and you're done

> 💡 No need to install Python beforehand — the wizard takes care of it.

> 🔒 Credentials and tokens are stored locally only. The token is passed to Python via an environment variable to avoid leaking through the process list.

## 📖 Usage

### Subscribe to a channel

Type a username or profile URL (`https://www.iwara.tv/profile/username`) into the URL box and press Enter. New uploads are checked automatically from then on.

### Download videos

- **From a channel** — select the channel in the tree → select videos → right-click → Download
- **Single video** — paste a video URL into the URL box and press Enter
- **Via clipboard** — enable clipboard monitoring, then just copy an iwara URL
- **Search import** — Tools → search import to bulk-import iwara search results

<details>
<summary><b>🔍 Search syntax</b></summary>

| Syntax | Meaning |
|--------|---------|
| `foo bar` | foo AND bar (each term matches title / artist / tags / memo) |
| `-bot` | exclude items containing "bot" |
| `tag:vr` | tags contain vr |
| `author:foo` | author name contains foo |
| `memo:fav` | memo contains "fav" |
| `status:failed` | status filter (aliases like `done` / `wip` / `err` work too) |
| `fav:true` | favorites only |
| `rating:nsfw` / `site:ai` / `id:xxx` | rating / site / VideoId filters |
| `"two words"` | treat the quoted text as a single term |

While a channel is selected, free-text terms intentionally skip the author field (it would match every video) and search title / tags instead.

</details>

<details>
<summary><b>⌨️ Keyboard shortcuts</b></summary>

| Key | Action |
|-----|--------|
| `F5` | Check for new videos |
| `Ctrl+D` | Download selected videos |
| `Ctrl+F` | Focus the search box |
| `Ctrl+A` | Select all |
| `Delete` | Remove selected videos |

</details>

<details>
<summary><b>📝 Filename template</b></summary>

| Variable | Example |
|----------|---------|
| `{title}` | My Video |
| `{author}` | username |
| `{date}` | 20250101 (post date) |
| `{id}` | AbCdEfGh |
| `{quality}` | Source |

Default: `{id}_{title}`

</details>

<details>
<summary><b>🏷️ Importing files without tags</b></summary>

You can make mp4 files you already have (e.g. downloaded with an external tool) that lack the iwara tag (mp4 only — m4v is not supported) recognized as "already downloaded". When a folder import finds untagged files, a dialog lets you pick a matching strategy.

| Method | Description |
|--------|-------------|
| Artist folder search | Explicitly designate one folder = one artist, then match only against that artist's videos (safest) |
| Filename search | Matches against the entire local DB. Providing path hints via a `{id}` `{title}` `{artist}` `{date}` template improves accuracy. `{date}` recognizes `yyyy-MM-dd`, `yyyy_MM_dd`, `yyyy.MM.dd`, and `yyyyMMdd` automatically |
| Skip | Import nothing |

Matching priority is: direct ID match → exact title match → substring match → prefix match. If a file has multiple candidates left after auto-matching, it's flagged for review in the grid, where you can pick the video to use from a dropdown.

A date-like prefix at the start of a filename (e.g. `2024-06-15_Title.mp4`) and fullwidth/halfwidth punctuation mismatches between the DB and the actual file (`（）` vs `()`, etc.) are both handled automatically, even without specifying a template.

</details>

<details>
<summary><b>🚦 Rate-limit settings</b></summary>

Tune these in Settings when subscribing to many channels or doing large bulk downloads.

| Setting | Default |
|---------|---------|
| API request interval | 1000 ms |
| Download interval | 3000 ms |
| Channel crawl interval | 5000 ms |
| Wait after 429/403 | 30000 ms |
| Exponential backoff | ON |

> Setting these too low may get you 403/429 responses from the server.

</details>

## 🌐 Web Media Server

1. Open Settings → the Media Server tab
2. Configure the port and authentication (recommended), then click Start
3. Open the displayed URL (e.g. `http://192.168.1.10:8080`) in a browser on your phone / PC

Player shortcuts: `Space` play/pause, `←` `→` seek 10 s, `↑` `↓` volume, `F` fullscreen, `N` / `P` next / previous video

## 💾 Data Locations

Everything is stored locally. Nothing is transmitted externally.

```
%APPDATA%\IwaraDownloader\
├── settings.json        # app settings
├── data.db              # subscriptions & videos (SQLite)
├── token.txt            # login token
├── thumbs/              # thumbnail cache
├── backups/             # daily DB backups (up to 7 generations)
└── logs/                # logs
```

A hidden `.iwara_index.json` is created in each download folder to speed up UUID matching scans.

## 🔧 Troubleshooting

<details>
<summary><b>Setup / login fails</b></summary>

- Check your internet connection (needed to fetch Python and install cloudscraper)
- If you pointed the wizard at an existing Python, verify the path (full path to `python.exe`)
- Confirm you can log in to iwara.tv directly
- Make sure your antivirus is not blocking the app

</details>

<details>
<summary><b>Downloads fail</b></summary>

- Check your login state and whether the video is private / deleted
- Check free disk space
- If you keep getting 403/429, increase the rate-limit values
- For Cloudflare errors, re-run the environment setup and retry later

</details>

<details>
<summary><b>The app takes a while to close</b></summary>

If you close it while downloads or mp4 tag writes are in progress, it waits for cleanup to finish to avoid corrupting files (a tray balloon notifies you).

</details>

## 🛠️ Building

```powershell
git clone https://github.com/dekotan24/iwara-downloader.git
cd iwara-downloader
dotnet build IwaraDownloader\IwaraDownloader.csproj -c Release
```

Builds with Visual Studio 2022 / the .NET 10.0 SDK.

Run the tests with:

```powershell
dotnet test IwaraDownloader.sln -c Debug
```

## 🧩 Tech Stack

| Area | Technology |
|------|------------|
| Desktop app | C# / WinForms (.NET 10.0) |
| Web server | ASP.NET Core Kestrel (Minimal API) + vanilla JS |
| Database | SQLite (Microsoft.Data.Sqlite) |
| iwara API | Python 3.10+ / [cloudscraper](https://github.com/VeNoMouS/cloudscraper) (Cloudflare bypass) |
| Metadata | [TagLibSharp](https://github.com/mono/taglib-sharp) (UUID embedding into mp4) |
| Misc | NAudio (notification sounds), DPAPI (credential encryption) |

## 📄 License

[MIT License](LICENSE)

## ⚠️ Disclaimer

- This software is intended for personal use
- Copyright of downloaded videos belongs to their respective owners
- The author assumes no responsibility for any damage caused by using this software
- Follow the terms of service of iwara.tv / iwara.ai

## 🙏 Acknowledgements

- [iwara-python-api](https://github.com/xiatg/iwara-python-api)
- [cloudscraper](https://github.com/VeNoMouS/cloudscraper)
- Parts of this project were developed with assistance from [Claude](https://claude.ai) by Anthropic
