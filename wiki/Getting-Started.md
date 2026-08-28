# インストール・初回セットアップ

## インストール

1. [Releases](https://github.com/dekotan24/iwara-downloader/releases) から最新版をダウンロード
2. 任意のフォルダに展開
3. `IwaraDownloader.exe` を実行

[.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) が必要です。未インストールの場合はインストールしてください。

## 初回セットアップウィザード

初回起動時にセットアップウィザードが自動で開きます。

### Step 1: Python 環境の選択

以下のいずれかを選択します。

- **自動ダウンロード（推奨）**: Python 3.10.11 embeddable を自動でダウンロード・展開します。事前に Python をインストールしておく必要はありません
- **既存の Python を指定**: インストール済みの Python 3.10 以上の `python.exe` のフルパスを指定します

### Step 2: セットアップ実行

ウィザードが以下を自動で行います。

1. Python のダウンロード・展開（自動ダウンロードを選択した場合）
2. pip のインストール
3. 必要なパッケージのインストール（cloudscraper, yt-dlp）

完了すると `.python_setup_done` マーカーファイルが作成されます。

### Step 3: ログイン

iwara.tv のメールアドレスとパスワードでログインします。

- パスワードは DPAPI で暗号化してローカルに保存されます
- JWT トークンは環境変数経由で Python に渡されます（プロセスリストからの漏洩防止）
- R-18 コンテンツやプライベート動画のダウンロードにはログインが必要です

## データの保存場所

すべてローカルに保存されます。外部へのデータ送信はありません。

```
%APPDATA%\IwaraDownloader\
├── settings.json        # アプリ設定
├── data.db              # 購読・動画情報 (SQLite)
├── token.txt            # ログイントークン
├── thumbs/              # サムネイルキャッシュ
├── logs/                # ログファイル
└── backups/             # DB バックアップ（日次、最大 7 世代）
```

動画の保存先フォルダには `.iwara_index.json`（UUID 照合キャッシュ）が作成されます。
