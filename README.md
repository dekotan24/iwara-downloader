<div align="center">

<img width="72" height="72" alt="icon" src="https://github.com/user-attachments/assets/dfae4206-78de-45de-a975-d9b69b96c68b" />

# IwaraDownloader

**iwara.tv / iwara.ai の動画ダウンローダー & メディアサーバー**

チャンネルを購読すれば新着を自動で保存し、内蔵の Web サーバーで手持ちの端末から視聴できる Windows アプリ。

[![Version](https://img.shields.io/badge/version-3.0.0-blue.svg)](https://github.com/dekotan24/iwara-downloader/releases)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6.svg)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

[English](README_en.md) · [ダウンロード](https://github.com/dekotan24/iwara-downloader/releases)

<img width="1000" alt="screenshot" src="https://github.com/user-attachments/assets/de36894c-65e9-41af-b150-e65c7ff4a8bf" />

</div>

## 特徴

- **チャンネル購読** — ユーザーを登録するだけで新着を自動検出してダウンロード
- **止まらないダウンロード** — HTTP Range によるレジューム、自動リトライ、起動時の自動再開、ログイン切れの検知とキュー再開
- **Web メディアサーバー内蔵** — LAN 内のスマホ・タブレットのブラウザから視聴、検索、お気に入り管理
- **強力な検索** — `tag:vr` `author:foo` `-除外語` `"フレーズ"` などのフィールド指定に対応した横断検索
- **コレクション管理** — mp4 に UUID を埋め込んで重複を防止、保存先移動、重複チェック、統計ダッシュボード
- **除外リスト** — 消した動画が新着チェックで復活しない。いつでも一覧に戻せる
- **多言語 UI** — 日本語 / English / 简体中文
- **ローカル保存** — ライブラリも認証情報もこの PC 内にのみ保存。作者や第三者のサーバーで預かることはありません（iwara との通信と更新確認は行います）

## 動作環境

| | |
|---|---|
| OS | Windows 10 / 11 (64bit) |
| ランタイム | [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Python | 不要（初回ウィザードが自動で用意。既存の Python 3.10+ も指定可） |

## はじめかた

1. [Releases](https://github.com/dekotan24/iwara-downloader/releases) から最新版を取得して展開
2. `IwaraDownloader.exe` を起動すると初回セットアップウィザードが開く
3. ウィザードに従うと Python と必要パッケージの準備まで自動で完了する
4. iwara.tv のアカウントでログイン

購読するには、URL 入力欄にユーザー名かプロフィール URL を入れて Enter。以降は新着チェックが自動で走ります。単発で落としたいときは動画 URL を貼るか、クリップボード監視を ON にしてコピーするだけ。

## Web メディアサーバー

設定 → メディアサーバー でポートと認証を設定して開始すると、表示された URL にブラウザからアクセスできます。

ストリーミング再生（シーク対応）、プレイリスト、連続再生、検索、お気に入り、DL キューの確認に対応。認証パスワードは DPAPI で暗号化して保存されます。

> [!WARNING]
> LAN 内での利用を想定しています。インターネットへ直接公開しないでください。

## データの保存場所

```
%APPDATA%\IwaraDownloader\
├── settings.json   アプリ設定
├── data.db         購読・動画情報 (SQLite)
├── token.txt       ログイントークン
├── thumbs/         サムネイルキャッシュ
├── backups/        DB 自動バックアップ (日次・最大 7 世代)
└── logs/           ログ
```

<details>
<summary>検索構文</summary>

| 構文 | 意味 |
|------|------|
| `foo bar` | foo AND bar（タイトル / アーティスト / タグ / メモを横断） |
| `-bot` | "bot" を含むものを除外 |
| `tag:vr` | タグ指定 |
| `author:foo` | 投稿者名指定 |
| `memo:推し` | メモ指定 |
| `status:failed` | ステータス指定（`done` / `wip` / `err` のエイリアス可） |
| `fav:true` | お気に入りのみ |
| `rating:nsfw` / `site:ai` / `id:xxx` | レーティング / サイト / VideoId 指定 |
| `"two words"` | 引用符内をひと塊として検索 |

</details>

<details>
<summary>ファイル名テンプレート</summary>

`{title}` `{author}` `{date}` `{id}` `{quality}` が使えます。デフォルトは `{id}_{title}`。

</details>

<details>
<summary>キーボードショートカット</summary>

| キー | 機能 |
|------|------|
| `F5` | 新着チェック |
| `Ctrl+D` | 選択動画をダウンロード |
| `Ctrl+F` | 検索ボックスにフォーカス |
| `Ctrl+A` | 全選択 |
| `Delete` | 選択動画を削除 |

</details>

<details>
<summary>うまく動かないとき</summary>

**セットアップ / ログインに失敗する**
インターネット接続、既存 Python を指定した場合はそのパス、iwara.tv に直接ログインできるか、ウイルス対策ソフトのブロックを順に確認してください。

**ダウンロードに失敗する**
ログイン状態と動画の公開状態、ディスク空き容量を確認してください。403 / 429 が頻発する場合は設定でレート制限の値を大きくします。Cloudflare エラーは環境セットアップを再実行して時間をおいてから再試行してください。

**終了に時間がかかる**
ダウンロード中や mp4 へのタグ書き込み中に閉じると、ファイル破損を防ぐため後始末の完了を待ちます。

</details>

## ビルド

```powershell
git clone https://github.com/dekotan24/iwara-downloader.git
cd iwara-downloader
dotnet build IwaraDownloader.sln -c Release
```

Visual Studio 2022 / .NET 10.0 SDK が必要です。ソリューションには本体と DB 操作ツールが含まれ、両方が同じ出力フォルダにビルドされます。

## 技術スタック

C# / WPF (.NET 10.0) · ASP.NET Core Kestrel + Vanilla JS · SQLite · Python 3.10+ と [cloudscraper](https://github.com/VeNoMouS/cloudscraper)（Cloudflare 回避） · [TagLibSharp](https://github.com/mono/taglib-sharp)（mp4 への UUID 埋め込み） · NAudio · DPAPI

## ライセンス

[MIT](LICENSE)

## 免責事項

個人利用を目的としたソフトウェアです。ダウンロードした動画の著作権は各権利者に帰属します。使用により生じた損害について作者は責任を負いません。iwara.tv / iwara.ai の利用規約を遵守してください。

## 謝辞

[iwara-python-api](https://github.com/xiatg/iwara-python-api) · [cloudscraper](https://github.com/VeNoMouS/cloudscraper) · 開発の一部に [Claude](https://claude.ai) by Anthropic を使用しています
