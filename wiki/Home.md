# IwaraDownloader Wiki

iwara.tv / iwara.ai 対応の動画ダウンローダー & メディアサーバーです。

チャンネルを購読すれば新着を自動で保存し、内蔵の Web サーバーで手持ちの端末から視聴できる Windows デスクトップアプリです。

## ページ一覧

| ページ | 内容 |
|--------|------|
| [インストール・初回セットアップ](Getting-Started) | ダウンロード、セットアップウィザード、ログイン、データの保存場所 |
| [基本的な使い方](Basic-Usage) | 画面構成、チャンネル購読、ダウンロード、インポート |
| [検索・フィルタ](Search-and-Filter) | 検索構文、NSFW フィルタ、表示切替 |
| [コレクション管理](Collection-Management) | UUID タグ、ファイル移動、重複チェック、リネーム、統計 |
| [Web メディアサーバー](Web-Media-Server) | 内蔵 Web サーバーの設定と使い方 |
| [DB 操作ツール](Database-Tool) | 上級者向けの DB メンテナンスツール |
| [設定一覧](Settings) | 全設定項目の説明 |
| [トラブルシューティング](Troubleshooting) | よくある問題と対処法 |
| [ソースからビルド](Building-from-Source) | ビルド方法とプロジェクト構成 |

## 動作環境

| 項目 | 要件 |
|------|------|
| OS | Windows 10 / 11 (64bit) |
| ランタイム | [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Python | 不要（初回セットアップウィザードが自動で取得）。既存の Python 3.10 以上を使うことも可 |
| 表示言語 | 日本語 / English / 简体中文（設定 → 一般 で切替。「自動」は OS の言語に追従） |

## 技術スタック

| 領域 | 技術 |
|------|------|
| アプリ本体 | C# / WPF (.NET 10.0)、MVVM (CommunityToolkit.Mvvm) |
| Web サーバー | ASP.NET Core Kestrel (Minimal API) + Vanilla JS |
| データベース | SQLite (Microsoft.Data.Sqlite) |
| iwara API | Python 3.10+ / cloudscraper（Cloudflare 回避） |
| メタデータ | TagLibSharp（mp4 への UUID 埋め込み） |
| その他 | NAudio（通知音）、DPAPI（資格情報の暗号化）、yt-dlp（外部動画 DL） |

## リンク

- [GitHub リポジトリ](https://github.com/dekotan24/iwara-downloader/)
- [Releases](https://github.com/dekotan24/iwara-downloader/releases)
- [ライセンス (MIT)](https://github.com/dekotan24/iwara-downloader/blob/main/LICENSE)
