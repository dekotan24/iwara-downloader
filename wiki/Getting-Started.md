# インストール・初回セットアップ

## インストール

1. [Releases](https://github.com/dekotan24/iwara-downloader/releases) から最新版をダウンロード
2. 任意のフォルダに展開
3. `IwaraDownloader.exe` を実行

[.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) が必要です。未インストールの場合はインストールしてください。

## 初回セットアップウィザード

初回起動時にセットアップウィザードが自動で開きます。

### Step 1: Rust helper の確認

以下のいずれかを選択します。

- **同梱helperを使用（推奨）**: アプリ同梱の `iwara-helper.exe` を使用します。追加のランタイムやライブラリは不要です
- **別のhelperを指定**: 別ビルドの `iwara-helper.exe` を使う場合のみ、その実行ファイルのフルパスを指定します

### Step 2: セットアップ実行

ウィザードが以下を自動で行います。

1. `iwara-helper.exe` の存在確認
2. Rust helperの設定を保存
3. セットアップ完了マーカーを作成

完了すると `.rust_setup_done` マーカーファイルが作成されます。

### Step 3: ログイン

iwara.tv のメールアドレスとパスワードでログインします。

- パスワードは DPAPI で暗号化してローカルに保存されます
- JWT トークンは環境変数経由でRust helperに渡されます（プロセスリストからの漏洩防止）
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
