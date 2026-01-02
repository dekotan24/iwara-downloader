# IwaraDownloader

[![Version](https://img.shields.io/badge/version-1.1.0-blue.svg)](https://github.com/dekotan24/iwara-downloader/releases)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D6.svg)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

iwara.tv から動画をダウンロードするためのWindows用デスクトップアプリケーションです。チャンネル購読と新着動画の自動検出機能を備えています。

**[English README](README.md)**

## 機能

- **チャンネル購読** - お気に入りのユーザーを登録して新着動画を自動チェック
- **一括ダウンロード** - 購読チャンネルの動画をまとめてダウンロード
- **単発ダウンロード** - 動画URLを指定して個別にダウンロード
- **ダウンロードキュー管理** - 複数動画の同時ダウンロード（最大3並列）
- **自動新着チェック** - 指定間隔で新着動画を自動検出
- **起動時リジューム** - 未完了のダウンロードを自動再開
- **統計ダッシュボード** - 動画数、成功率、日別推移を確認
- **URL一括インポート** - テキストやファイルから複数URLを一括追加
- **重複チェック** - 同一動画の重複検出・削除
- **ファイル一括リネーム** - テンプレートによる一括リネーム
- **通知音** - ダウンロード完了/エラー時に音で通知
- **タスクトレイ常駐** - バックグラウンドで動作
- **トースト通知** - ダウンロード完了や新着検出を通知

## 動作環境

| 項目 | 要件 |
|------|------|
| OS | Windows 10/11 (64bit) |
| ランタイム | [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |
| Python | 3.8以上（3.10〜3.13で動作確認済） |

## インストール

### 1. ダウンロード

[Releases](https://github.com/dekotan24/iwara-downloader/releases) から最新版をダウンロードして任意のフォルダに展開してください。

### 2. Python環境の準備

Python 3.8以上がインストールされている必要があります。

- [Python公式サイト](https://www.python.org/downloads/) からダウンロード
- または [Python Embeddable Package](https://www.python.org/downloads/) を使用

### 3. 初回セットアップ

1. `IwaraDownloader.exe` を起動
2. 「環境セットアップ」ボタンをクリック
3. Pythonのパスを入力（例: `C:\Python311\python.exe`）
4. セットアップが完了するまで待機（cloudscraperが自動インストールされます）

### 4. ログイン

1. 「ログイン」ボタンをクリック
2. iwara.tv のメールアドレスとパスワードを入力
3. ログイン完了後、すべての機能が利用可能になります

## 使い方

### チャンネルを購読する

1. URL入力欄にユーザー名またはプロフィールURL（`https://www.iwara.tv/profile/username`）を入力
2. Enterキーを押すか「追加」ボタンをクリック
3. 左側のチャンネルリストに追加されます

### 動画をダウンロードする

**チャンネルの動画をダウンロード:**
1. 左側のチャンネルリストからチャンネルを選択
2. 右側の動画リストでダウンロードしたい動画を選択（複数選択可）
3. 右クリック →「ダウンロード」を選択

**単発でダウンロード:**
1. URL入力欄に動画URL（`https://www.iwara.tv/video/xxxxx`）を入力
2. Enterキーを押すとダウンロードキューに追加されます

**URL一括インポート:**
1. メニュー →「URL一括インポート」を選択
2. テキストエリアにURLを貼り付け、またはファイルから読み込み
3. 「インポート」ボタンで一括追加

### キーボードショートカット

| ショートカット | 機能 |
|--------------|-----|
| `F5` | 新着チェック |
| `Ctrl+D` | 選択した動画をダウンロード |
| `Ctrl+F` | フィルターボックスにフォーカス |
| `Ctrl+A` | 動画リストの全選択 |
| `Delete` | 選択した動画を削除 |

## 設定

### 基本設定

| 項目 | 説明 | デフォルト |
|------|------|-----------|
| ダウンロード先フォルダ | 動画の保存先 | マイビデオ/Iwara |
| デフォルト画質 | Source / 540p / 360p | Source |
| 同時ダウンロード数 | 1〜3（1-2推奨） | 2 |
| リトライ回数 | 失敗時の再試行回数 | 3 |
| 自動チェック間隔 | 新着チェックの間隔 | 60分 |

### ファイル名テンプレート

以下の変数が使用可能です：

| 変数 | 説明 | 例 |
|------|------|-----|
| `{title}` | 動画タイトル | My Video |
| `{author}` | 投稿者名 | username |
| `{date}` | 投稿日 | 2025-01-01 |
| `{id}` | 動画ID | AbCdEfGh |
| `{quality}` | 画質 | Source |

デフォルト: `{id}_{title}`

### 詳細設定（レート制限）

多数のチャンネルを購読している場合や、大量の動画をダウンロードする場合に調整できます：

| 項目 | 説明 | デフォルト |
|------|------|-----------|
| APIリクエスト間隔 | 動画情報取得等のリクエスト間隔 | 1000ms |
| ダウンロード間隔 | 動画DL完了後の待機時間 | 3000ms |
| チャンネル巡回間隔 | 次のチャンネルチェックまでの待機 | 5000ms |
| ページ取得間隔 | 動画一覧のページング時 | 500ms |
| 429/403エラー時の待機 | エラー時のバックオフ時間 | 30000ms |
| エクスポネンシャルバックオフ | 連続エラー時に待機時間を段階的に増加 | ON |

> ⚠️ 値が小さすぎるとサーバーからアクセス制限（403/429エラー）を受ける可能性があります。

## ファイル構成

```
IwaraDownloader/
├── IwaraDownloader.exe    # メインアプリケーション
├── iwara_helper.py        # Python APIヘルパー
├── iwara_setup.bat        # Python環境セットアップ用
├── task_complete.mp3      # 完了音（デフォルト）
├── task_error.mp3         # エラー音（デフォルト）
└── その他DLL等
```

## データ保存場所

アプリケーションデータは以下に保存されます：

```
%APPDATA%\IwaraDownloader\
├── settings.json         # アプリ設定
├── data.db               # 購読・動画情報（SQLite）
├── token.txt             # ログイントークン
├── python_path.txt       # Pythonパス設定
└── logs/                 # ログファイル
    └── IwaraDownloader_YYYYMMDD_HHMMSS.log
```

## トラブルシューティング

### セットアップに失敗する

- Pythonのパスが正しいか確認してください
- インターネット接続を確認してください（cloudscraperのインストールに必要）
- ウイルス対策ソフトがブロックしていないか確認してください
- Pythonのバージョンが3.8以上か確認してください

### ログインに失敗する

- メールアドレスとパスワードが正しいか確認してください
- iwara.tv のサイトに直接ログインできるか確認してください
- 環境セットアップが完了しているか確認してください

### ダウンロードに失敗する

- ログイン状態を確認してください
- 動画が非公開または削除されていないか確認してください
- ディスク容量を確認してください
- 403エラーが頻発する場合はレート制限の値を大きくしてください

### Cloudflareエラーが発生する

- 環境セットアップを再実行してください
- しばらく時間をおいてから再試行してください
- curl_cffiがインストールされているか確認してください（オプション）

## 依存ライブラリ

### Python
- [cloudscraper](https://github.com/VeNoMouS/cloudscraper) - Cloudflare対策
- [curl_cffi](https://github.com/yifeikong/curl_cffi) - TLSフィンガープリント偽装（オプション）

### .NET
- Microsoft.Data.Sqlite - SQLiteデータベース
- System.Text.Json - JSON処理
- System.Security.Cryptography.ProtectedData - パスワード暗号化
- NAudio - 音声再生

## ライセンス

MIT License - 詳細は [LICENSE](LICENSE) をご覧ください。

## 免責事項

- 本ソフトウェアは個人利用を目的としています
- ダウンロードした動画の著作権は各権利者に帰属します
- 本ソフトウェアの使用により生じたいかなる損害・損失についても、作者は責任を負いません
- iwara.tv の利用規約を遵守してご使用ください

## 謝辞

このプロジェクトは以下のプロジェクトを参考に構築されています：

- [iwara-python-api](https://github.com/xiatg/iwara-python-api)
- [cloudscraper](https://github.com/VeNoMouS/cloudscraper)

また、以下のツールによってコーディングされました：
- [Claude](https://claude.ai) by Anthropic

## 更新履歴

### v1.1.0 (2026-01-03)
- スプラッシュスクリーン追加
- URL一括インポート機能
- 重複チェック機能
- 統計ダッシュボード
- ファイル一括リネーム機能
- ダウンロード完了音/エラー音
- GitHub更新チェック機能
- 起動時リジューム機能
- ファイル名テンプレート機能
- カラムソート機能
- ツリービューに「全ての動画」「未DL」「DL済」ノード追加
- 右クリックメニューが1回目で開かない問題を修正
- ダウンロード進捗の同期ズレを修正
- 403エラーの詳細判別（レート制限 vs 権限不足）
- curl_cffi対応（Cloudflare対策強化）
- ログ出力機能追加

### v1.0.0
- 初回リリース
