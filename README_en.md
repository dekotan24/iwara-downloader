<div align="center">

<img width="72" height="72" alt="icon" src="https://github.com/user-attachments/assets/dfae4206-78de-45de-a975-d9b69b96c68b" />

# IwaraDownloader

**A video downloader and media server for iwara.tv / iwara.ai**

Subscribe to a channel and new uploads are saved automatically. Watch them from any device on your network through the built-in web server.

[![Version](https://img.shields.io/badge/version-3.0.0-blue.svg)](https://github.com/dekotan24/iwara-downloader/releases)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6.svg)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

[日本語](README.md) · [Download](https://github.com/dekotan24/iwara-downloader/releases)

<img width="1000" alt="screenshot" src="https://github.com/user-attachments/assets/de36894c-65e9-41af-b150-e65c7ff4a8bf" />

</div>

## Features

- **Channel subscriptions** — add a user and new uploads are detected and downloaded for you
- **Downloads that keep going** — HTTP Range resume, automatic retries, resume on startup, and session-expiry detection that pauses and restarts the queue
- **Built-in web media server** — watch, search and manage your library from a phone or tablet on your LAN
- **Real search** — field-scoped queries across your library: `tag:vr`, `author:foo`, `-excluded`, `"exact phrase"`
- **Collection management** — UUIDs embedded in the mp4 prevent re-downloads, plus library relocation, duplicate checks, and a statistics dashboard
- **Exclusion list** — deleted videos stay deleted instead of reappearing on the next check, and can be restored at any time
- **Multilingual UI** — English / 日本語 / 简体中文
- **Entirely local** — files and credentials never leave your machine

## Requirements

| | |
|---|---|
| OS | Windows 10 / 11 (64-bit) |
| Runtime | [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Python | Not required — the setup wizard installs it (an existing Python 3.10+ also works) |

## Getting started

1. Grab the latest build from [Releases](https://github.com/dekotan24/iwara-downloader/releases) and extract it
2. Run `IwaraDownloader.exe` — the setup wizard opens on first launch
3. Follow the wizard; it prepares Python and the required packages for you
4. Sign in with your iwara.tv account

To subscribe, type a username or profile URL into the address box and press Enter. To grab a single video, paste its URL — or turn on clipboard monitoring and just copy it.

## Web media server

Open Settings → Media Server, set a port and credentials, then start it and open the URL it shows in any browser.

It supports streaming with seeking, playlists, continuous playback, search, favorites, and download queue status. The password is stored encrypted with DPAPI.

> [!WARNING]
> This is meant for your local network. Do not expose it directly to the internet.

## Where data is stored

```
%APPDATA%\IwaraDownloader\
├── settings.json   application settings
├── data.db         subscriptions and video records (SQLite)
├── token.txt       login token
├── thumbs/         thumbnail cache
├── backups/        automatic DB backups (daily, up to 7 kept)
└── logs/           logs
```

<details>
<summary>Search syntax</summary>

| Syntax | Meaning |
|--------|---------|
| `foo bar` | foo AND bar (matches title / artist / tag / memo) |
| `-bot` | exclude anything containing "bot" |
| `tag:vr` | match a tag |
| `author:foo` | match the uploader |
| `memo:note` | match your memo |
| `status:failed` | match status (aliases such as `done` / `wip` / `err` work) |
| `fav:true` | favorites only |
| `rating:nsfw` / `site:ai` / `id:xxx` | rating / site / video id |
| `"two words"` | treat the quoted text as one term |

</details>

<details>
<summary>Filename template</summary>

`{title}`, `{author}`, `{date}`, `{id}` and `{quality}` are available. The default is `{id}_{title}`.

</details>

<details>
<summary>Keyboard shortcuts</summary>

| Key | Action |
|-----|--------|
| `F5` | Check for new uploads |
| `Ctrl+D` | Download selection |
| `Ctrl+F` | Focus the search box |
| `Ctrl+A` | Select all |
| `Delete` | Delete selection |

</details>

<details>
<summary>Troubleshooting</summary>

**Setup or login fails**
Check your internet connection, the path if you pointed at an existing Python, whether you can sign in to iwara.tv directly, and whether your antivirus is blocking the app.

**Downloads fail**
Check that you are still signed in, that the video is still public, and that you have free disk space. If you see frequent 403 / 429 responses, raise the rate-limit values in the settings. For Cloudflare errors, re-run the environment setup and try again later.

**Closing the app takes a while**
If you close it during a download or while UUID tags are being written to an mp4, it waits for that work to finish so files are not corrupted.

</details>

## Building

```powershell
git clone https://github.com/dekotan24/iwara-downloader.git
cd iwara-downloader
dotnet build IwaraDownloader.sln -c Release
dotnet test IwaraDownloader.sln -c Debug
```

Visual Studio 2022 and the .NET 10.0 SDK are required.

## Built with

C# / WPF (.NET 10.0) · ASP.NET Core Kestrel + vanilla JS · SQLite · Python 3.10+ with [cloudscraper](https://github.com/VeNoMouS/cloudscraper) for Cloudflare · [TagLibSharp](https://github.com/mono/taglib-sharp) for embedding UUIDs in mp4 files · NAudio · DPAPI

## License

[MIT](LICENSE)

## Disclaimer

This software is intended for personal use. Copyright in downloaded videos remains with the respective rights holders. The author accepts no liability for any damage arising from its use. Please follow the terms of service of iwara.tv / iwara.ai.

## Acknowledgements

[iwara-python-api](https://github.com/xiatg/iwara-python-api) · [cloudscraper](https://github.com/VeNoMouS/cloudscraper) · Parts of this project were developed with [Claude](https://claude.ai) by Anthropic
