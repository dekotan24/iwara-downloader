# IwaraDownloader

[![Version](https://img.shields.io/badge/version-1.1.0-blue.svg)](https://github.com/dekotan24/iwara-downloader/releases)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D6.svg)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

A Windows desktop application for downloading videos from iwara.tv with channel subscription and automatic new video detection.

**[日本語版 README はこちら](README_ja.md)**

## Features

- **Channel Subscription** - Subscribe to your favorite users and automatically check for new videos
- **Batch Download** - Download multiple videos from subscribed channels at once
- **Single Video Download** - Download individual videos by URL
- **Download Queue** - Manage multiple concurrent downloads (up to 3 parallel)
- **Auto Check** - Automatically detect new videos at specified intervals
- **Resume on Startup** - Automatically resume incomplete downloads
- **Statistics Dashboard** - View download statistics, success rates, and daily trends
- **Bulk URL Import** - Import multiple URLs from text or file
- **Duplicate Check** - Detect and manage duplicate videos
- **Batch Rename** - Rename downloaded files using customizable templates
- **Notification Sound** - Play sound on download completion or error
- **System Tray** - Run in background with tray icon
- **Toast Notifications** - Get notified on download completion and new video detection

## Requirements

| Item | Requirement |
|------|-------------|
| OS | Windows 10/11 (64-bit) |
| Runtime | [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |
| Python | 3.8 or higher (tested on 3.10) |

## Installation

### 1. Download

Download the latest release from [Releases](https://github.com/dekotan24/iwara-downloader/releases) and extract to any folder.

### 2. Python Setup

Ensure Python 3.8+ is installed:
- Download from [Python Official Site](https://www.python.org/downloads/)
- Or use [Python Embeddable Package](https://www.python.org/downloads/)

### 3. Initial Setup

1. Run `IwaraDownloader.exe`
2. Click "Environment Setup" button
3. Enter your Python path (e.g., `C:\Python311\python.exe`)
4. Wait for setup to complete (cloudscraper will be installed automatically)

### 4. Login

1. Click "Login" button
2. Enter your iwara.tv email and password
3. After successful login, all features become available

## Usage

### Subscribe to a Channel

1. Enter username or profile URL (`https://www.iwara.tv/profile/username`) in the URL input field
2. Press Enter or click "Add" button
3. The channel will appear in the left panel

### Download Videos

**From Channel:**
1. Select a channel from the left panel
2. Select videos to download (multi-select supported)
3. Right-click → "Download"

**Single Video:**
1. Enter video URL (`https://www.iwara.tv/video/xxxxx`) in the URL input field
2. Press Enter to add to download queue

**Bulk Import:**
1. Menu → "Bulk URL Import"
2. Paste URLs or load from file
3. Click "Import"

### Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `F5` | Check for new videos |
| `Ctrl+D` | Download selected videos |
| `Ctrl+F` | Focus filter box |
| `Ctrl+A` | Select all videos |
| `Delete` | Delete selected videos |

## Configuration

### Basic Settings

| Setting | Description | Default |
|---------|-------------|---------|
| Download Folder | Video save location | My Videos/Iwara |
| Default Quality | Source / 540p / 360p | Source |
| Concurrent Downloads | 1-3 (1-2 recommended) | 2 |
| Retry Count | Number of retry attempts | 3 |
| Check Interval | New video check interval | 60 min |

### Filename Template

Available variables:

| Variable | Description | Example |
|----------|-------------|---------|
| `{title}` | Video title | My Video |
| `{author}` | Uploader name | username |
| `{date}` | Upload date | 2025-01-01 |
| `{id}` | Video ID | AbCdEfGh |
| `{quality}` | Quality | Source |

Default: `{id}_{title}`

### Rate Limiting

For heavy users with many subscriptions:

| Setting | Description | Default |
|---------|-------------|---------|
| API Request Delay | Delay between API requests | 1000ms |
| Download Delay | Delay after each download | 3000ms |
| Channel Check Delay | Delay between channel checks | 5000ms |
| Page Fetch Delay | Delay when paginating | 500ms |
| Rate Limit Base Delay | Base delay on 429/403 error | 30000ms |
| Exponential Backoff | Increase delay on consecutive errors | ON |

> ⚠️ Setting values too low may result in access restrictions (403/429 errors).

## File Structure

```
IwaraDownloader/
├── IwaraDownloader.exe    # Main application
├── iwara_helper.py        # Python API helper
├── iwara_setup.bat        # Python environment setup
├── task_complete.mp3      # Completion sound (default)
├── task_error.mp3         # Error sound (default)
└── [DLLs and other files]
```

## Data Location

Application data is stored in:

```
%APPDATA%\IwaraDownloader\
├── settings.json         # App settings
├── data.db               # Subscriptions and video info (SQLite)
├── token.txt             # Login token
├── python_path.txt       # Python path setting
└── logs/                 # Log files
    └── IwaraDownloader_YYYYMMDD_HHMMSS.log
```

## Troubleshooting

### Setup Fails

- Verify Python path is correct
- Check internet connection (required for cloudscraper installation)
- Check if antivirus is blocking the process
- Ensure Python version is 3.8 or higher

### Login Fails

- Verify email and password are correct
- Check if you can login directly on iwara.tv
- Ensure environment setup is complete

### Download Fails

- Check login status
- Verify the video is not private or deleted
- Check disk space
- If 403 errors occur frequently, increase rate limit values

### Cloudflare Errors

- Run environment setup again
- Wait a while and retry
- Check if curl_cffi was installed (optional, for better Cloudflare bypass)

## Dependencies

### Python
- [cloudscraper](https://github.com/VeNoMouS/cloudscraper) - Cloudflare bypass
- [curl_cffi](https://github.com/yifeikong/curl_cffi) - TLS fingerprint spoofing (optional)

### .NET
- Microsoft.Data.Sqlite - SQLite database
- System.Text.Json - JSON processing
- System.Security.Cryptography.ProtectedData - Password encryption
- NAudio - Audio playback

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Disclaimer

- This software is intended for personal use only
- Copyright of downloaded videos belongs to their respective owners
- The author is not responsible for any damages or losses caused by the use of this software
- Please comply with iwara.tv's terms of service

## Acknowledgments

This project is built with reference to:

- [iwara-python-api](https://github.com/xiatg/iwara-python-api)
- [cloudscraper](https://github.com/VeNoMouS/cloudscraper)

Coded with assistance from:
- [Claude](https://claude.ai) by Anthropic

