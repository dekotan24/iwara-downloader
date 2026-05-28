# IwaraDownloader

[![Version](https://img.shields.io/badge/version-2.0.0-blue.svg)](https://github.com/dekotan24/iwara-downloader/releases)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D6.svg)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

iwara.tv / iwara.ai から動画をダウンロードするための Windows 用デスクトップアプリケーションです。チャンネル購読・新着自動検出・レジューム・サムネビュー等を備えています。

**[English README](README_en.md)**

## 主な機能

### ダウンロード
- **チャンネル購読** - お気に入りユーザーを登録して新着動画を自動チェック
- **一括ダウンロード** - 購読チャンネルの動画をまとめて DL
- **単発ダウンロード** - 動画 URL を指定して個別 DL
- **キュー管理** - 複数動画の同時 DL（最大 3 並列）
- **自動新着チェック** - 指定間隔で新着動画を自動検出
- **レジューム** - 通信切れや一時停止後、HTTP Range で続きから再開（file_id / サイズ / ETag で整合性検証）
- **起動時自動再開** - 未完了の DL をアプリ起動時に再開
- **iwara.ai 対応** - URL から自動でサイトを判別（iwara.tv / iwara.ai）

### UI / 利便性
- **サムネ表示モード** - 詳細リスト ↔ タイル（サムネ）を切替
- **クリップボード監視** - iwara の URL をコピーすると自動でキューに追加
- **動画検索インポート** - iwara の検索 API 経由で結果を一括取り込み
- **NSFW フィルタ** - All / SFW / NSFW で表示絞り込み
- **リッチ右クリックメニュー** - 再生 / フォルダを開く / 投稿者ページ / 再 DL / ファイル存在チェック / 詳細情報 / URL コピー
- **ダブルクリック再生** - 完了動画はローカルファイルを開く、未完了なら iwara のページへ
- **全体進捗** - ステータスバーに DL 中タスクの平均進捗を表示
- **タスクトレイ常駐** - バックグラウンドで動作、トースト通知

### 管理
- **重複チェック** - 同一動画の重複検出・削除
- **UUID ベース重複検出** - mp4 に iwara UUID を埋め込み、ファイル名や DB が変わっても重複 DL を防止
- **既存ファイル一括タグ書き込み** - 過去の DL 済みファイルへバックグラウンドで UUID タグを書き込み
- **サムネ一括補完** - 既存動画のサムネをバックグラウンドで一括取得
- **ファイル一括リネーム** - テンプレートで一括リネーム
- **URL 一括インポート** - テキスト / ファイルから複数 URL を追加
- **統計ダッシュボード** - 動画数、成功率、日別推移

## 動作環境

| 項目 | 要件 |
|------|------|
| OS | Windows 10/11 (64bit) |
| ランタイム | [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |
| Python | 3.8 以上（3.10 で動作確認済） |

## インストール

### 1. ダウンロード

[Releases](https://github.com/dekotan24/iwara-downloader/releases) から最新版をダウンロードして任意のフォルダに展開してください。

### 2. Python 環境

Python 3.8 以上が必要です（[Python 公式](https://www.python.org/downloads/)）。Embeddable Package でも可。

### 3. 初回セットアップ

1. `IwaraDownloader.exe` を起動
2. 「環境セットアップ」ボタンをクリック
3. Python のパスを入力（例: `C:\Python311\python.exe`）
4. cloudscraper 等の必要パッケージが自動インストールされます

### 4. ログイン

1. 「ログイン」ボタンをクリック
2. iwara.tv のメールアドレスとパスワードを入力（資格情報はローカルにのみ保存。トークンは環境変数経由で Python に渡し、プロセスリスト経由の漏洩を防ぎます）
3. ログイン完了後、すべての機能が利用可能になります

> ⚠️ **v1.1.1 以降ログインが必須です。** トークンの有効期限は起動時に自動検証され、期限切れなら自動ログアウトします。

## 使い方

### チャンネル購読
1. URL 入力欄にユーザー名またはプロフィール URL（`https://www.iwara.tv/profile/username`）を入力
2. Enter または「追加」をクリック

### 動画ダウンロード
**チャンネルから:**
1. 左のチャンネルリストから選択
2. 右の動画リストで対象を選択（複数選択可）
3. 右クリック →「ダウンロード」

**単発:**
- URL 入力欄に `https://www.iwara.tv/video/xxxxx`（または `iwara.ai`）を入力 → Enter

**クリップボード経由:**
- ツールバーの「クリップボード監視」を ON にすると、iwara の URL をコピーするだけで自動追加

**検索インポート:**
- メニュー →「検索インポート」で iwara の検索結果を一括取り込み

### キーボードショートカット

| キー | 機能 |
|------|------|
| `F5` | 新着チェック |
| `Ctrl+D` | 選択動画を DL |
| `Ctrl+F` | フィルターにフォーカス |
| `Ctrl+A` | 全選択 |
| `Delete` | 選択動画を削除 |

## 設定

### 基本設定

| 項目 | 説明 | デフォルト |
|------|------|-----------|
| 保存先フォルダ | 動画の保存先 | マイビデオ/Iwara |
| デフォルト画質 | Source / 540p / 360p | Source |
| 同時 DL 数 | 1〜3（1〜2 推奨） | 2 |
| リトライ回数 | 失敗時の再試行 | 3 |
| 自動チェック間隔 | 新着チェック | 60 分 |

### ファイル名テンプレート

| 変数 | 例 |
|------|-----|
| `{title}` | My Video |
| `{author}` | username |
| `{date}` | 2025-01-01 |
| `{id}` | AbCdEfGh |
| `{quality}` | Source |

デフォルト: `{id}_{title}`

### レート制限

多数のチャンネルを購読 / 大量 DL する場合に調整できます。

| 項目 | デフォルト |
|------|-----------|
| API リクエスト間隔 | 1000ms |
| ダウンロード間隔 | 3000ms |
| チャンネル巡回間隔 | 5000ms |
| ページ取得間隔 | 500ms |
| 429/403 時の待機 | 30000ms |
| エクスポネンシャルバックオフ | ON |

> ⚠️ 値を小さくしすぎると 403/429 を受ける可能性があります。

## データ保存場所

すべてローカルのみに保存され、外部送信は行いません。

```
%APPDATA%\IwaraDownloader\
├── settings.json             # アプリ設定
├── data.db                   # 購読・動画情報 (SQLite)
├── token.txt                 # ログイントークン
├── python_path.txt           # Python パス設定
├── x_version_secret.txt      # iwara X-Version シークレット (30日キャッシュ)
├── thumbs/                   # サムネキャッシュ
└── logs/                     # ログ
```

各保存フォルダ内には `.iwara_index.json` が作成され、UUID 照合スキャンを高速化します。

## トラブルシューティング

### セットアップに失敗する
- Python のパスが正しいか確認
- インターネット接続を確認（cloudscraper のインストールに必要）
- ウイルス対策ソフトがブロックしていないか確認

### ログインに失敗する
- メールアドレスとパスワードが正しいか確認
- iwara.tv に直接ログインできるか確認
- 環境セットアップが完了しているか確認

### ダウンロードに失敗する
- ログイン状態を確認
- 動画が非公開・削除されていないか
- ディスク容量を確認
- 403/429 が頻発する場合はレート制限の値を大きく

### 全動画が 360p でしか DL できない（v1.1.0 以前）
v1.1.1 で恒久対策済み（X-Version シークレットの 3 段階フォールバック）。

### Cloudflare エラーが発生する
- 環境セットアップを再実行
- しばらく時間をおいて再試行

## 依存ライブラリ

### Python
- [cloudscraper](https://github.com/VeNoMouS/cloudscraper) - Cloudflare 対策

### .NET
- Microsoft.Data.Sqlite
- System.Text.Json
- System.Security.Cryptography.ProtectedData
- NAudio
- TagLibSharp

## ライセンス

MIT License - 詳細は [LICENSE](LICENSE) を参照。

## 免責事項

- 本ソフトウェアは個人利用を目的としています
- DL した動画の著作権は各権利者に帰属します
- 使用により生じた損害について作者は責任を負いません
- iwara.tv / iwara.ai の利用規約を遵守してください

## 謝辞

- [iwara-python-api](https://github.com/xiatg/iwara-python-api)
- [cloudscraper](https://github.com/VeNoMouS/cloudscraper)
- 一部のコーディング補助に [Claude](https://claude.ai) by Anthropic を使用しています
