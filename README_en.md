<div align="center">

<img width="72" height="72" alt="icon" src="https://github.com/user-attachments/assets/dfae4206-78de-45de-a975-d9b69b96c68b" />

# IwaraDownloader

**A Windows app to collect, organize, and watch videos from iwara.tv / iwara.ai**

[![Version](https://img.shields.io/badge/version-3.1.0-blue.svg)](https://github.com/dekotan24/iwara-downloader/releases)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6.svg)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

[Download](https://github.com/dekotan24/iwara-downloader/releases) · [日本語](README.md)

<img width="1186" height="743" alt="screenshot" src="https://github.com/user-attachments/assets/8686f4d7-6107-4843-9597-24da7beefc1c" />

</div>

## Features

### Collect
- **Channel subscriptions** — Register a creator and new uploads are checked periodically and downloaded automatically
- **Just paste a URL** — Enter a video URL, turn on clipboard monitoring (copying a URL is enough), bulk-import a list of URLs, or import from iwara search results
- **Videos hosted outside iwara** — Embedded videos such as YouTube are saved with yt-dlp (on/off as a default and per channel)

### Download
- **Downloads that keep going** — HTTP Range resume, automatic retries, exponential backoff, and resuming unfinished downloads at startup
- **Priority queue** — Set Highest / High / Normal / Low per video or per channel
- **Gentle on the server** — Separate intervals for API calls, page fetches, downloads, and channel checks, with presets
- **Free space limit** — No new downloads start once free space drops below the limit you set

### Organize
- **A library without duplicates** — The iwara video ID is embedded in each mp4, so a video can be found again even after renaming the file or losing the database
- **Import existing files** — Scan a folder and register the videos you already have (matching can use your filename template)
- **Exclusion list** — Deleted videos don't come back on the next check, and can be restored later
- **List and thumbnail views** — Switch between a detailed list and tiles, and filter by tag, NSFW, or keyword
- **Maintenance tools** — Duplicate check, statistics dashboard, bulk move and relink of files, integrity check, and daily database backups

### Watch
- **Built-in web media server** — Stream, search, favorite, and check download status from a phone or tablet browser on the same LAN

The UI is available in Japanese, English, and Simplified Chinese.

## Requirements

| | |
|---|---|
| OS | Windows 10 / 11 (64-bit) |
| Runtime | [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Python | No prior install needed (set up automatically on first launch; you can also point it to your own Python 3.10+) |

## Getting started

1. Download the latest zip from [Releases](https://github.com/dekotan24/iwara-downloader/releases) and extract it anywhere
2. Launch `IwaraDownloader.exe` — the first-run setup wizard opens
3. Follow the wizard; it prepares Python and the required packages for you
4. Log in to your iwara account under Settings → Account (needed for R-18 and private videos)

## Usage

**Subscribe to a channel**
Type a creator's username or profile URL into the input box at the top and press Enter. New uploads are then checked automatically and queued.

**Save a single video**
Paste a video URL into the input box and press Enter. With clipboard monitoring on, copying the URL in your browser is enough.

**Add many at once**
The Tools menu offers:

| Menu | What it does |
|---|---|
| Bulk URL import | Paste many video URLs and register them together. Canceling midway keeps what was already fetched |
| iwara search import | Pick videos from iwara search results |
| Import from folder | Register files you have already downloaded |

Right-click a video to play it, open its folder, re-download, refresh its info, change its priority, favorite, delete, or restore it. Right-click a channel to change its save folder, default priority, and how it handles videos hosted outside iwara.

<details>
<summary>Tools menu</summary>

| Menu | What it does |
|---|---|
| Duplicate check | Find files that point to the same video |
| Statistics dashboard | Totals for your library |
| Move unrelocated files | After changing the save folder, move files left in the old location |
| Relink moved files | Find files you moved by hand and update their paths in the database |
| Local file integrity check | Find mismatches between the database and the files on disk |
| Repair imported video information | Re-fetch channel and filename info missing from bulk imports |
| Database tool (advanced) | SQL editor and table browser. Shown only when enabled in Settings |

</details>

## Web media server

Under Settings → Media Server, choose a port, username, and password, then start the server and open the displayed URL in a browser. To watch from other devices, turn on "Allow access from other devices on the LAN".

It supports seekable streaming, playlists with continuous playback, search, favorites, and checking downloads and errors. The password is stored encrypted with Windows DPAPI.

> [!WARNING]
> This feature is meant for use on your LAN. Do not expose it directly to the internet.

## Search syntax

Works in the app's search box (the web media server's search is a simple space-separated AND search).

| Syntax | Meaning |
|---|---|
| `foo bar` | Contains both foo and bar (searches title / creator / tags / memo) |
| `-bot` | Excludes items containing bot |
| `"two words"` | Searches the quoted text as one phrase |
| `tag:vr` | Filter by tag |
| `author:foo` | Filter by creator name |
| `title:foo` / `memo:foo` | Filter by title / memo |
| `status:failed` | Filter by status (short forms such as `done` `wip` `wait` `err` `skip` `pause` also work) |
| `fav:true` | Favorites only |
| `rating:ecchi` / `site:ai` / `id:xxx` | Filter by rating (`general` / `ecchi`) / site / video ID |

Field filters can be negated too (e.g. `-tag:vr`).

## Filename template

Set the filename format under Settings → Other. The default is `{id}_{title}`.

| Placeholder | Value |
|---|---|
| `{title}` | Video title |
| `{author}` | Creator's username |
| `{date}` | Upload date (`yyyyMMdd`) |
| `{id}` | Video ID |
| `{quality}` | Quality |

## Where data is stored

All settings and library data are stored in the folder below and never sent to the author's or any third-party server (the app only talks to iwara, and to GitHub to check for updates).

```
%APPDATA%\IwaraDownloader\
├── settings.json   App settings
├── data.db         Subscriptions and videos (SQLite)
├── token.txt       Login token
├── thumbs\         Thumbnail cache
├── backups\        Automatic database backups (once a day, 7 kept)
└── logs\           Logs
```

## Troubleshooting

<details>
<summary>Setup or login fails</summary>

Check your internet connection, whether you can log in on the iwara website directly, and whether antivirus software is blocking the app. If you specified your own Python, check that path as well.

</details>

<details>
<summary>Downloads fail</summary>

Check that you are logged in, that the video is public, and that the disk has free space. If 403 / 429 errors keep appearing, increase the wait times under Settings → Advanced. For Cloudflare errors, run the environment setup again and retry after a while.

</details>

<details>
<summary>Closing takes a while</summary>

If you close the app while downloading or while writing info into an mp4, it waits for cleanup to finish so files are not corrupted.

</details>

Logs can be opened from Help → Open Log Folder.

## Building from source

```powershell
git clone https://github.com/dekotan24/iwara-downloader.git
cd iwara-downloader
dotnet build IwaraDownloader.sln -c Release
```

Requires the .NET 10.0 SDK. The solution contains the main app and the database tool, both built into the same output folder.

## Tech stack

C# / WPF (.NET 10.0) · ASP.NET Core Kestrel + Vanilla JS · SQLite · Python 3.10+ / [cloudscraper](https://github.com/VeNoMouS/cloudscraper) · [TagLibSharp](https://github.com/mono/taglib-sharp) · NAudio · yt-dlp

## License

[MIT](LICENSE)

## Disclaimer

This software is intended for personal use. Copyright in downloaded videos belongs to their respective owners. Please follow the iwara.tv / iwara.ai terms of service. The author is not responsible for any damage resulting from the use of this software.

## Acknowledgements

[iwara-python-api](https://github.com/xiatg/iwara-python-api) · [cloudscraper](https://github.com/VeNoMouS/cloudscraper) · [Claude Code](https://claude.ai) · [Codex](https://chatgpt.com)
