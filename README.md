<div align="center">

<img width="72" height="72" alt="icon" src="https://github.com/user-attachments/assets/dfae4206-78de-45de-a975-d9b69b96c68b" />

# IwaraDownloader

**iwara.tv / iwara.ai の動画を集めて、整理して、どこからでも観るための Windows アプリ**

[![Version](https://img.shields.io/badge/version-3.1.0-blue.svg)](https://github.com/dekotan24/iwara-downloader/releases)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6.svg)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

[ダウンロード](https://github.com/dekotan24/iwara-downloader/releases) · [English](README_en.md)

<img width="1186" height="743" alt="screenshot" src="https://github.com/user-attachments/assets/8686f4d7-6107-4843-9597-24da7beefc1c" />

</div>

## できること

### 集める
- **チャンネル購読** — 投稿者を登録しておけば、新着を定期的にチェックして自動でダウンロード
- **URL を貼るだけ** — 動画 URL の入力、クリップボード監視（コピーしただけで追加）、URL の一括インポート、iwara の検索結果からのインポート
- **iwara 外の動画にも対応** — YouTube 埋め込みなどは yt-dlp で保存（既定値とチャンネルごとに ON/OFF）

### 落とす
- **止まらないダウンロード** — HTTP Range によるレジューム、自動リトライ、エクスポネンシャルバックオフ、起動時の未完了 DL 再開
- **優先度つきキュー** — 動画ごと・チャンネルごとに「最優先 / 優先 / 通常 / 低」を指定
- **サーバーにやさしい** — API・ページ取得・ダウンロード・チャンネル巡回の間隔をそれぞれ調整でき、プリセットも用意
- **空き容量の下限** — 指定容量を切ったら新しい DL を始めない

### 整理する
- **重複しないライブラリ** — mp4 に iwara の動画 ID を埋め込むので、ファイル名を変えても DB が消えても同じ動画を見つけ直せる
- **既存ファイルの取り込み** — フォルダをスキャンして、手元の動画を DB に登録（ファイル名テンプレートからの推定にも対応）
- **除外リスト** — 消した動画は新着チェックで復活しない。あとから復元も可能
- **一覧とサムネイル表示** — 詳細リストとタイルを切り替え、タグ・NSFW・キーワードで絞り込み
- **メンテナンス用ツール** — 重複チェック、統計ダッシュボード、ファイルの一括移動・再リンク、整合性チェック、DB の日次バックアップ

### 観る
- **Web メディアサーバー内蔵** — 同じ LAN のスマホやタブレットのブラウザから、ストリーミング再生・検索・お気に入り・DL 状況の確認ができる

UI は日本語 / English / 简体中文 に対応しています。

## 動作環境

| | |
|---|---|
| OS | Windows 10 / 11（64bit） |
| ランタイム | [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Python | 事前のインストールは不要（初回セットアップで自動的に用意します。手持ちの Python 3.10 以降を指定することもできます） |

## はじめかた

1. [Releases](https://github.com/dekotan24/iwara-downloader/releases) から最新の zip を取得して、好きな場所に展開します
2. `IwaraDownloader.exe` を起動すると、初回セットアップウィザードが開きます
3. ウィザードを進めると、Python と必要なパッケージの準備まで自動で終わります
4. 設定 → アカウント で iwara のアカウントにログインします（R-18 やプライベート動画の取得に必要です）

## 使い方

**チャンネルを購読する**
画面上部の入力欄に、投稿者のユーザー名かプロフィール URL を入れて Enter。以降は新着チェックが自動で走り、見つかった動画がキューに入ります。

**動画を 1 本だけ保存する**
入力欄に動画 URL を貼って Enter。クリップボード監視を ON にしておけば、ブラウザで URL をコピーするだけで追加されます。

**まとめて追加する**
ツール メニューから次の方法が選べます。

| メニュー | 内容 |
|---|---|
| URL 一括インポート | 複数の動画 URL を貼り付けてまとめて登録。途中でキャンセルしても取得済みの分は残ります |
| iwara 検索インポート | iwara の検索結果から選んで登録 |
| フォルダから取り込み | 既にダウンロード済みのファイルを DB に登録 |

一覧の右クリックから、再生・フォルダを開く・再ダウンロード・情報の再取得・優先度の変更・お気に入り・削除 / 復元などが行えます。チャンネル側の右クリックでは、保存先・既定の優先度・iwara 外動画の扱いをチャンネルごとに変更できます。

<details>
<summary>ツール メニュー一覧</summary>

| メニュー | 内容 |
|---|---|
| 重複チェック | 同じ動画を指しているファイルを探す |
| 統計ダッシュボード | ライブラリの件数・容量などの集計 |
| 未移動ファイルの一括移動 | 保存先を変えたあと、古い場所に残ったファイルを新しい保存先へ移す |
| 移動済みファイルの再リンク | 手で移動したファイルを探して DB のパスを付け直す |
| ローカルファイル整合性チェック | DB とディスク上のファイルの食い違いを調べる |
| URL 一括インポート済み動画の情報を修復 | 一括インポートで欠けたチャンネルやファイル名の情報を取り直す |
| DB 操作ツール（上級者向け） | SQL エディタとテーブルブラウザ。設定で有効にしたときだけ表示されます |

</details>

## Web メディアサーバー

設定 → メディアサーバー でポート・ユーザー名・パスワードを決めて開始すると、表示された URL にブラウザからアクセスできます。ほかの端末から観るときは「LAN 内の他デバイスからのアクセスを許可」を ON にしてください。

シーク可能なストリーミング再生、プレイリストと連続再生、検索、お気に入り、ダウンロード状況・エラーの確認に対応しています。パスワードは Windows の DPAPI で暗号化して保存されます。

> [!WARNING]
> LAN 内で使う前提の機能です。インターネットに直接公開しないでください。

## 検索構文

アプリの検索欄で使えます（Web メディアサーバーの検索はスペース区切りの AND 検索です）。

| 書き方 | 意味 |
|---|---|
| `foo bar` | foo と bar の両方を含む（タイトル / 投稿者 / タグ / メモが対象） |
| `-bot` | bot を含むものを除く |
| `"two words"` | 引用符の中をひとまとまりとして探す |
| `tag:vr` | タグで絞る |
| `author:foo` | 投稿者名で絞る |
| `title:foo` / `memo:foo` | タイトル / メモで絞る |
| `status:failed` | 状態で絞る（`done` `wip` `wait` `err` `skip` `pause` などの短縮形も可） |
| `fav:true` | お気に入りだけ |
| `rating:ecchi` / `site:ai` / `id:xxx` | レーティング（`general` / `ecchi`） / サイト / 動画 ID で絞る |

フィールド指定にも `-` を付けられます（例: `-tag:vr`）。

## ファイル名テンプレート

設定 → その他 で保存するファイル名の形を決められます。既定は `{id}_{title}` です。

| プレースホルダ | 中身 |
|---|---|
| `{title}` | 動画タイトル |
| `{author}` | 投稿者のユーザー名 |
| `{date}` | 投稿日（`yyyyMMdd`） |
| `{id}` | 動画 ID |
| `{quality}` | 画質 |

## データの保存場所

設定やライブラリの情報は、すべて次のフォルダに保存されます。作者や第三者のサーバーに送られることはありません（通信するのは iwara と、更新確認のための GitHub だけです）。

```
%APPDATA%\IwaraDownloader\
├── settings.json   アプリの設定
├── data.db         購読・動画の情報（SQLite）
├── token.txt       ログイントークン
├── thumbs\         サムネイルのキャッシュ
├── backups\        DB の自動バックアップ（1 日 1 回・7 世代）
└── logs\           ログ
```

## うまく動かないとき

<details>
<summary>セットアップやログインに失敗する</summary>

インターネットに接続できているか、iwara のサイトに直接ログインできるか、ウイルス対策ソフトがブロックしていないかを確認してください。手持ちの Python を指定している場合は、そのパスも確認してください。

</details>

<details>
<summary>ダウンロードに失敗する</summary>

ログインしているか、動画が公開されているか、ディスクに空きがあるかを確認してください。403 / 429 エラーが続くときは、設定 → 詳細設定 で待機時間を長くします。Cloudflare のエラーが出るときは、環境セットアップをやり直して、しばらく時間をおいてから再試行してください。

</details>

<details>
<summary>終了に時間がかかる</summary>

ダウンロード中や mp4 への情報の書き込み中に閉じると、ファイルが壊れないように後始末が終わるのを待ってから終了します。

</details>

ログは ヘルプ → ログフォルダを開く から確認できます。

## ソースからビルド

```powershell
git clone https://github.com/dekotan24/iwara-downloader.git
cd iwara-downloader
dotnet build IwaraDownloader.sln -c Release
```

.NET 10.0 SDK が必要です。ソリューションには本体と DB 操作ツールが含まれていて、どちらも同じ出力フォルダにビルドされます。

## 技術スタック

C# / WPF（.NET 10.0） · ASP.NET Core Kestrel + Vanilla JS · SQLite · Python 3.10+ / [cloudscraper](https://github.com/VeNoMouS/cloudscraper) · [TagLibSharp](https://github.com/mono/taglib-sharp) · NAudio · yt-dlp

## ライセンス

[MIT](LICENSE)

## 免責事項

個人で使うためのソフトウェアです。ダウンロードした動画の著作権は、それぞれの権利者にあります。iwara.tv / iwara.ai の利用規約を守って使ってください。このソフトウェアの使用によって生じた損害について、作者は責任を負いません。

## 謝辞

[iwara-python-api](https://github.com/xiatg/iwara-python-api) · [cloudscraper](https://github.com/VeNoMouS/cloudscraper) · [Claude Code](https://claude.ai) · [Codex](https://chatgpt.com)
