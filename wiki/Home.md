# IwaraDownloader Wiki

iwara.tv / iwara.ai 対応の動画ダウンローダー & メディアサーバーです。

チャンネル購読 → 新着自動ダウンロード → ブラウザで視聴、までを 1 本で行える Windows デスクトップアプリです。

## ページ一覧

| ページ | 内容 |
|--------|------|
| [インストール・初回セットアップ](Getting-Started) | ダウンロード、セットアップウィザード、ログイン |
| [基本的な使い方](Basic-Usage) | チャンネル購読、ダウンロード、インポート |
| [検索・フィルタ](Search-and-Filter) | 検索構文、NSFW フィルタ、表示切替 |
| [コレクション管理](Collection-Management) | UUID タグ、ファイル移動、重複チェック、リネーム、統計 |
| [Web メディアサーバー](Web-Media-Server) | 内蔵 Web サーバーの設定と使い方 |
| [設定一覧](Settings) | 全設定項目の説明 |
| [トラブルシューティング](Troubleshooting) | よくある問題と対処法 |
| [ソースからビルド](Building-from-Source) | ビルド方法 |

## 動作環境

| 項目 | 要件 |
|------|------|
| OS | Windows 10 / 11 (64bit) |
| ランタイム | [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Rust helper | アプリに `iwara-helper.exe` を同梱。Pythonやpipは不要 |

## 技術スタック

| 領域 | 技術 |
|------|------|
| アプリ本体 | C# / WinForms (.NET 10.0) |
| Web サーバー | ASP.NET Core Kestrel (Minimal API) + Vanilla JS |
| データベース | SQLite (Microsoft.Data.Sqlite) |
| iwara API | 同梱Rust `iwara-helper.exe`（reqwest + rustls） |
| メタデータ | TagLibSharp（mp4 への UUID 埋め込み） |
| その他 | NAudio（通知音）、DPAPI（資格情報の暗号化）、yt-dlp（外部動画 DL） |

## リンク

- [GitHub リポジトリ](https://github.com/dekotan24/iwara-downloader/)
- [Releases](https://github.com/dekotan24/iwara-downloader/releases)
- [ライセンス (MIT)](https://github.com/dekotan24/iwara-downloader/blob/main/LICENSE)
