# IwaraDownloader

[![Version](https://img.shields.io/badge/version-2.0.0-blue.svg)](https://github.com/dekotan24/iwara-downloader/releases)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D6.svg)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

A Windows desktop app for downloading videos from iwara.tv / iwara.ai with channel subscriptions, automatic new-video detection, resume, thumbnail view, and more.

**[日本語 README](README.md)**

## Key Features

### Downloading
- **Channel subscriptions** – follow your favorite uploaders and auto-check for new uploads
- **Bulk download** – queue all videos from a channel at once
- **Single download** – paste a video URL to add it
- **Queue management** – up to 3 parallel downloads
- **Scheduled checks** – automatic new-video detection at a configurable interval
- **Resume** – continue interrupted downloads via HTTP Range, validated with file_id / size / ETag
- **Auto-resume on launch** – pending downloads restart automatically when the app starts
- **iwara.ai support** – site is auto-detected from the URL (iwara.tv / iwara.ai)

### UI / Convenience
- **Thumbnail view** – toggle between detail list and tile (thumbnail) mode
- **Clipboard watcher** – copy an iwara URL to enqueue it automatically
- **Search import** – pull results from the iwara search API and import in bulk
- **NSFW filter** – All / SFW / NSFW filtering
- **Rich context menu** – play / open folder / open author page / re-download / file-exists check / details / copy URL
- **Double-click play** – completed videos open locally, otherwise open the iwara page
- **Overall progress** – the status bar shows the average progress of active downloads
- **System tray** – runs in the background with toast notifications

### Management
- **Duplicate check** – detect and remove duplicate videos
- **UUID-based deduplication** – embeds the iwara UUID in the mp4, so dedupe survives file renames and DB loss
- **Backfill UUID tags on existing files** – write iwara UUIDs onto previously downloaded files in the background
- **Backfill thumbnails** – fetch thumbnails for existing videos in the background
- **Batch rename** – template-based renaming
- **Bulk URL import** – add multiple URLs from text or a file
- **Statistics dashboard** – totals, success rate, daily trends

## Requirements

| Item | Requirement |
|------|-------------|
| OS | Windows 10/11 (64-bit) |
| Runtime | [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |
| Python | 3.8+ (tested on 3.10) |

## Installation

### 1. Download

Download the latest release from [Releases](https://github.com/dekotan24/iwara-downloader/releases) and extract to any folder.

### 2. Python environment

Python 3.8 or later is required ([python.org](https://www.python.org/downloads/)). The Embeddable Package works as well.

### 3. First-time setup

1. Launch `IwaraDownloader.exe`
2. Click **Environment Setup**
3. Enter your Python path (e.g. `C:\Python311\python.exe`)
4. Required packages (cloudscraper, etc.) are installed automatically

### 4. Login

1. Click **Login**
2. Enter your iwara.tv email and password (credentials are stored locally only; the token is passed to Python via an environment variable to prevent leakage through the process list)
3. All features are available after login

> ⚠️ **Login is required since v1.1.1.** The token's expiration is verified at startup; if expired, the app logs out automatically.

## Usage

### Subscribe to a channel
1. Type a username or profile URL (`https://www.iwara.tv/profile/username`) into the URL bar
2. Press Enter or click **Add**

### Download videos
**From a channel:**
1. Pick a channel from the left list
2. Select videos in the right list (multi-select supported)
3. Right-click → **Download**

**Single video:**
- Paste `https://www.iwara.tv/video/xxxxx` (or `iwara.ai`) into the URL bar → Enter

**Via clipboard:**
- Turn on **Clipboard watcher** in the toolbar; copying an iwara URL automatically enqueues it

**Search import:**
- Menu → **Search import** to pull iwara search results in bulk

### Keyboard shortcuts

| Key | Action |
|------|--------|
| `F5` | Check for new uploads |
| `Ctrl+D` | Download selected |
| `Ctrl+F` | Focus the filter box |
| `Ctrl+A` | Select all |
| `Delete` | Remove selected |

## Settings

### General

| Item | Description | Default |
|------|-------------|---------|
| Save folder | Where videos go | My Videos/Iwara |
| Default quality | Source / 540p / 360p | Source |
| Concurrent downloads | 1–3 (1–2 recommended) | 2 |
| Retry count | On failure | 3 |
| Auto-check interval | New-upload polling | 60 min |

### Filename template

| Variable | Example |
|----------|---------|
| `{title}` | My Video |
| `{author}` | username |
| `{date}` | 2025-01-01 |
| `{id}` | AbCdEfGh |
| `{quality}` | Source |

Default: `{id}_{title}`

### Rate limits

Tune these if you subscribe to many channels or download a lot.

| Item | Default |
|------|---------|
| API request interval | 1000ms |
| Download interval | 3000ms |
| Channel poll interval | 5000ms |
| Page fetch interval | 500ms |
| Wait on 429/403 | 30000ms |
| Exponential backoff | ON |

> ⚠️ Values that are too small can trigger 403/429.

## Data locations

Everything is stored locally — nothing is sent externally.

```
%APPDATA%\IwaraDownloader\
├── settings.json             # app settings
├── data.db                   # subscriptions / videos (SQLite)
├── token.txt                 # login token
├── python_path.txt           # Python path config
├── x_version_secret.txt      # iwara X-Version secret (30-day cache)
├── thumbs/                   # thumbnail cache
└── logs/                     # logs
```

Each save folder also gets an `.iwara_index.json` to speed up UUID-based scanning.

## Troubleshooting

### Setup fails
- Check the Python path
- Check your internet connection (required to install cloudscraper)
- Make sure antivirus isn't blocking it

### Login fails
- Verify your email/password
- Confirm you can log into iwara.tv directly
- Confirm environment setup completed

### Downloads fail
- Confirm you are logged in
- Check the video isn't private or removed
- Check free disk space
- Raise rate-limit values if you see frequent 403/429

### Everything downloads as 360p (≤ v1.1.0)
Fixed for good in v1.1.1 (three-stage fallback for the X-Version secret).

### Cloudflare errors
- Re-run environment setup
- Wait a while and retry

## Dependencies

### Python
- [cloudscraper](https://github.com/VeNoMouS/cloudscraper) – Cloudflare bypass

### .NET
- Microsoft.Data.Sqlite
- System.Text.Json
- System.Security.Cryptography.ProtectedData
- NAudio
- TagLibSharp

## License

MIT License — see [LICENSE](LICENSE).

## Disclaimer

- This software is intended for personal use
- Copyrights of downloaded videos belong to their respective owners
- The author is not liable for any damages or losses arising from use of this software
- Please follow the terms of use of iwara.tv / iwara.ai

## Credits

- [iwara-python-api](https://github.com/xiatg/iwara-python-api)
- [cloudscraper](https://github.com/VeNoMouS/cloudscraper)
- Some coding was assisted by [Claude](https://claude.ai) by Anthropic
