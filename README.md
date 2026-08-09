<div align="center">

# <img width="32" height="32" alt="icon" src="https://github.com/user-attachments/assets/dfae4206-78de-45de-a975-d9b69b96c68b" /> IwaraDownloader

**iwara.tv / iwara.ai 対応の高機能動画ダウンローダー & メディアサーバー for Windows**

[![Version](https://img.shields.io/badge/version-2.5.0-blue.svg)](https://github.com/dekotan24/iwara-downloader/releases)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6.svg)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

チャンネル購読 → 新着自動ダウンロード → ブラウザでどこからでも視聴。<br>
コレクションの管理までこれ 1 本で完結する Windows デスクトップアプリです。

**[English README](README_en.md)**

[主な機能](#-主な機能) • [インストール](#-インストール) • [使い方](#-使い方) • [Web メディアサーバー](#-web-メディアサーバー) • [トラブルシューティング](#-トラブルシューティング)

</div>

---

<img width="1000" height="652" alt="2026-06-12_14h30_40" src="https://github.com/user-attachments/assets/de36894c-65e9-41af-b150-e65c7ff4a8bf" />

## ✨ ハイライト

- 📺 **チャンネル購読** — お気に入りユーザーを登録するだけで、新着を自動検出して自動ダウンロード
- 🔄 **堅牢なダウンロード** — レジューム（HTTP Range + 整合性検証）、自動リトライ、起動時の自動再開、ログイン切れ時のキュー自動停止・再開
- 🌐 **Web メディアサーバー内蔵** — スマホやタブレットのブラウザから LAN 内で視聴・管理（日本語 UI）
- ⭐ **お気に入り・強力な検索** — アプリと Web の両方からお気に入り管理、タイトル/アーティスト/タグの混合検索
- 🧰 **コレクション管理** — UUID ベースの重複検出、保存先移動（クラッシュ復旧付き）、統計ダッシュボード
- 🗑️ **除外リスト（ゴミ箱）** — 削除した動画は自動取得で復活せず、いつでも復元可能
- 🌐 **多言語対応** — 日本語 / English / 简体中文（設定でいつでも切替）
- 🔒 **完全ローカル** — データはすべてローカル保存。外部へのデータ送信は一切ありません

## 📋 主な機能

### 📥 ダウンロード

| 機能 | 説明 |
|------|------|
| チャンネル購読 | ユーザー名 / プロフィール URL を追加するだけ。チャンネルごとに保存先を個別設定可 |
| 新着自動チェック | 指定間隔で自動巡回。タイムアウト・指数バックオフ・優先キュー付きの直列ワーカーで安定動作 |
| 単発ダウンロード | 動画 URL を入力して Enter。クリップボード監視を ON にすればコピーするだけで追加 |
| 一括インポート | URL リスト / iwara 検索結果 / ローカルフォルダから一括取り込み。iwara タグの無い mp4 (対象は mp4 のみ) もファイル名やフォルダ構成から DB 上の動画と照合し、取り込み済みとして認識可能 |
| レジューム | 通信断・一時停止後も HTTP Range で続きから再開（file_id / サイズ / ETag で整合性検証） |
| 並列ダウンロード | 最大 3 並列 + レート制限設定（API 間隔 / DL 間隔 / 429 時の待機など） |
| iwara.ai 対応 | URL からサイトを自動判別 |

### ⭐ 整理・検索

- **お気に入り** — 一覧の右クリック / サムネの ★ / Web からトグル。ツリーの「⭐ お気に入り」で即一覧表示
- **混合検索** — タイトル / アーティスト / タグ / メモを 1 つの検索ボックスで横断検索
- **検索構文** — `tag:vr` `author:foo` `status:failed` `fav:true` `-除外語` `"フレーズ"` などのフィールド指定・除外に対応
- **NSFW フィルタ** — All / SFW / NSFW で表示切替
- **サムネビュー** — 詳細リスト ⇄ タイル表示の切替、サムネは自動取得・キャッシュ

### 📁 ファイル・コレクション管理

- **UUID 埋め込み** — mp4 に iwara の UUID をタグとして埋め込み。ファイル名や DB が変わっても重複 DL を防止
- **保存先の変更とファイル移動** — 容量チェック → 進捗表示付き移動。ジャーナル方式でクラッシュ・強制終了からも自動復旧
- **未移動ファイルの一括移動** — 容量不足などで移動に失敗したファイルを、原因解消後にまとめて再移動
- **移動済みファイルの再リンク** — FastCopy 等の外部ツールで移動した後、検証（サイズ → UUID）して DB のパスだけ追従
- **除外リスト（ゴミ箱）** — 動画を削除すると除外リストへ移動し、チャンネルの新着チェックで再び取得されるのを防止。ツリーの「🚫 除外済み」から復元・完全削除が可能
- **重複チェック / 一括リネーム / 統計ダッシュボード**（動画数・成功率・日別推移）

### 🌐 Web メディアサーバー

アプリに内蔵された Web サーバーを起動すると、**LAN 内のブラウザから視聴・管理**できます。

- 🎞️ ストリーミング再生（シーク対応）、プレイリスト / 連続再生 / シャッフル / キーボードショートカット
- 🔍 検索（スペース区切り AND）・ソート・グリッド / リスト表示・チャンネル別表示
- ⭐ お気に入りの登録 / 解除・お気に入りビュー
- 🚦 エラー一覧からの再試行・DL キュー状況・統計
- 🔐 ユーザー名 + パスワード認証（パスワードは DPAPI で暗号化保存、セッション 24 時間）

> ⚠️ LAN 内公開を想定しています。インターネットへの直接公開はしないでください。

## 🖥️ 動作環境

| 項目 | 要件 |
|------|------|
| OS | Windows 10 / 11 (64bit) |
| ランタイム | [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Python | 不要（初回セットアップウィザードが自動で取得）。既存の Python 3.10 以上を使うことも可 |
| 表示言語 | 日本語 / English / 简体中文（設定 → 一般 で切替。「自動」選択時は OS の言語に追従） |

## 🚀 インストール

1. **ダウンロード** — [Releases](https://github.com/dekotan24/iwara-downloader/releases) から最新版を取得して任意のフォルダに展開
2. **起動** — `IwaraDownloader.exe` を実行すると、初回はセットアップウィザードが自動で開きます
3. **ウィザードに従う** — 案内どおりに進めるだけで、Python の取得から必要パッケージのインストールまで自動で完了します（インストール済みの Python を指定することも可能）
4. **ログイン** — iwara.tv のメールアドレスとパスワードでログインすればセットアップ完了

> 💡 Python を事前にインストールしておく必要はありません。ウィザードが自動で用意します。

> 🔒 資格情報・トークンはローカルにのみ保存されます。トークンは環境変数経由で Python に渡され、プロセスリストからの漏洩を防ぎます。

## 📖 使い方

### チャンネルを購読する

URL 入力欄にユーザー名またはプロフィール URL（`https://www.iwara.tv/profile/username`）を入力して Enter。以降は新着チェックが自動で走ります。

### ダウンロードする

- **チャンネルから** — 左のツリーでチャンネル選択 → 動画を選んで右クリック →「ダウンロード」
- **単発** — URL 入力欄に動画 URL を入れて Enter
- **クリップボード経由** — 「クリップボード監視」を ON → iwara の URL をコピーするだけ
- **検索インポート** — ツール →「iwara 検索インポート」で検索結果を一括取り込み

<details>
<summary><b>🔍 検索構文の詳細</b></summary>

| 構文 | 意味 |
|------|------|
| `foo bar` | foo AND bar（各語がタイトル / アーティスト / タグ / メモのいずれかにマッチ） |
| `-bot` | "bot" を含むものを除外 |
| `tag:vr` | タグに vr を含む |
| `author:foo` | 投稿者名に foo を含む |
| `memo:推し` | メモに「推し」を含む |
| `status:failed` | ステータス指定（`done` / `wip` / `err` 等のエイリアス可） |
| `fav:true` | お気に入りのみ |
| `rating:nsfw` / `site:ai` / `id:xxx` | レーティング / サイト / VideoId 指定 |
| `"two words"` | 引用符内をひと塊として検索 |

アーティスト選択中は作者名がノイズになるため、フリーテキストは自動でタイトル / タグ中心の検索になります。

</details>

<details>
<summary><b>⌨️ キーボードショートカット</b></summary>

| キー | 機能 |
|------|------|
| `F5` | 新着チェック |
| `Ctrl+D` | 選択動画をダウンロード |
| `Ctrl+F` | 検索ボックスにフォーカス |
| `Ctrl+A` | 全選択 |
| `Delete` | 選択動画を削除 |

</details>

<details>
<summary><b>📝 ファイル名テンプレート</b></summary>

| 変数 | 例 |
|------|-----|
| `{title}` | My Video |
| `{author}` | username |
| `{date}` | 20250101 (投稿日) |
| `{id}` | AbCdEfGh |
| `{quality}` | Source |

デフォルト: `{id}_{title}`

</details>

<details>
<summary><b>🏷️ タグの無いファイルの取り込み</b></summary>

外部ツールなどで既に手元にある、iwara タグの無い mp4（対象は mp4 のみ、m4v は非対応）を「取り込み済み」として認識させることができます。フォルダ取り込みでタグ無しファイルが見つかると、検索方法を選ぶダイアログが表示されます。

| 方法 | 内容 |
|------|------|
| アーティストフォルダ選択検索 | フォルダ = 1 アーティストを明示指定し、そのアーティストの動画一覧だけを対象に照合（最も安全） |
| ファイル名による検索 | ローカル DB 全体を対象に照合。`{id}` `{title}` `{artist}` `{date}` テンプレートでパス構成のヒントを与えると精度が上がる。`{date}` は `yyyy-MM-dd` / `yyyy_MM_dd` / `yyyy.MM.dd` / `yyyyMMdd` を自動認識 |
| スキップ | 何も取り込まない |

照合優先順位は「ID 直接一致 → タイトル完全一致 → 部分一致 → 接頭辞一致」。1 ファイルに複数候補が残って自動確定できない場合は要確認としてグリッドに表示され、ドロップダウンから採用する動画を選び直せます。

ファイル名先頭の日付らしきプレフィックス（`2024-06-15_タイトル.mp4` 等）と、DB 側と実ファイル側で全角/半角が食い違う記号（`（）` と `()` 等）は、テンプレートを指定しなくても自動的に吸収されます。

</details>

<details>
<summary><b>🚦 レート制限の設定</b></summary>

大量の購読 / 一括 DL をする場合は設定画面で調整できます。

| 項目 | デフォルト |
|------|-----------|
| API リクエスト間隔 | 1000ms |
| ダウンロード間隔 | 3000ms |
| チャンネル巡回間隔 | 5000ms |
| 429/403 時の待機 | 30000ms |
| 指数バックオフ | ON |

> 値を小さくしすぎるとサーバーから 403/429 を受ける可能性があります。

</details>

## 🌐 Web メディアサーバー

1. 設定 →「メディアサーバー」タブを開く
2. ポート・認証（推奨）を設定して「開始」
3. 表示された URL（例: `http://192.168.1.10:8080`）にスマホ / PC のブラウザでアクセス

プレーヤーのキー操作: `Space` 再生/停止、`←` `→` 10 秒シーク、`↑` `↓` 音量、`F` フルスクリーン、`N` / `P` 次 / 前の動画

## 💾 データ保存場所

すべてローカルのみ。外部送信はありません。

```
%APPDATA%\IwaraDownloader\
├── settings.json        # アプリ設定
├── data.db              # 購読・動画情報 (SQLite)
├── token.txt            # ログイントークン
├── thumbs/              # サムネイルキャッシュ
├── backups/             # DB 自動バックアップ (日次・最大 7 世代)
└── logs/                # ログ
```

動画の保存フォルダには隠しファイル `.iwara_index.json` が作られ、UUID 照合スキャンを高速化します。

## 🔧 トラブルシューティング

<details>
<summary><b>セットアップ / ログインに失敗する</b></summary>

- インターネット接続を確認（Python の取得と cloudscraper のインストールに必要）
- 既存の Python を指定した場合はパスが正しいか確認（`python.exe` のフルパス）
- iwara.tv に直接ログインできるか確認
- ウイルス対策ソフトがブロックしていないか確認

</details>

<details>
<summary><b>ダウンロードに失敗する</b></summary>

- ログイン状態と動画の公開状態（削除・非公開）を確認
- ディスク空き容量を確認
- 403/429 が頻発する場合はレート制限の値を大きくする
- Cloudflare エラーは環境セットアップの再実行 + 時間をおいて再試行

</details>

<details>
<summary><b>終了に時間がかかる</b></summary>

ダウンロード中・mp4 タグ書き込み中に閉じると、ファイル破損を防ぐため後始末の完了を待ちます（トレイバルーンで通知されます）。

</details>

## 🛠️ ビルド

```powershell
git clone https://github.com/dekotan24/iwara-downloader.git
cd iwara-downloader
dotnet build IwaraDownloader\IwaraDownloader.csproj -c Release
```

Visual Studio 2022 / .NET 10.0 SDK でビルドできます。

テストは次のコマンドで実行できます。

```powershell
dotnet test IwaraDownloader.sln -c Debug
```

## 🧩 技術スタック

| 領域 | 技術 |
|------|------|
| アプリ本体 | C# / WinForms (.NET 10.0) |
| Web サーバー | ASP.NET Core Kestrel (Minimal API) + Vanilla JS |
| データベース | SQLite (Microsoft.Data.Sqlite) |
| iwara API | Python 3.10+ / [cloudscraper](https://github.com/VeNoMouS/cloudscraper)（Cloudflare 回避） |
| メタデータ | [TagLibSharp](https://github.com/mono/taglib-sharp)（mp4 への UUID 埋め込み） |
| その他 | NAudio（通知音）、DPAPI（資格情報の暗号化） |

## 📄 ライセンス

[MIT License](LICENSE)

## ⚠️ 免責事項

- 本ソフトウェアは個人利用を目的としています
- ダウンロードした動画の著作権は各権利者に帰属します
- 使用により生じた損害について作者は責任を負いません
- iwara.tv / iwara.ai の利用規約を遵守してください

## 🙏 謝辞

- [iwara-python-api](https://github.com/xiatg/iwara-python-api)
- [cloudscraper](https://github.com/VeNoMouS/cloudscraper)
- 開発の一部に [Claude](https://claude.ai) by Anthropic を使用しています
