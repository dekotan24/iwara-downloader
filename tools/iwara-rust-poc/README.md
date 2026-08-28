# iwara-helper (Rust)

旧 `IwaraDownloader/iwara_helper.py` のRust置換実装です。C#から1 action=1プロセスで呼び出し、stdoutのJSON契約とstderrの進捗契約を維持します。

## Build

WindowsではMSVC targetを推奨します。リポジトリルートから実行してください。

```powershell
cargo test --manifest-path tools/iwara-rust-poc/Cargo.toml
cargo build --release --manifest-path tools/iwara-rust-poc/Cargo.toml
```

生成された `target/release/iwara-helper.exe` をアプリの実行フォルダへ配置します。正式ビルドでは `IwaraDownloader/iwara-helper.exe` として同梱します。

## Commands

```text
iwara-helper login [email] [password]
iwara-helper verify_token
iwara-helper get_videos USERNAME
iwara-helper search QUERY [PAGE] [LIMIT]
iwara-helper get_url VIDEO_ID [QUALITY]
iwara-helper download VIDEO_ID OUTPUT_PATH
iwara-helper download_external EMBED_URL OUTPUT_PATH --yt-dlp-path PATH

# 検証用
iwara-helper get-video VIDEO_ID
iwara-helper download-test DIRECT_URL [OUTPUT_PATH]
iwara-helper download-test-video VIDEO_ID [QUALITY]
iwara-helper probe URL
```

認証情報は `IWARA_EMAIL`、`IWARA_PASSWORD`、`IWARA_TOKEN` で渡せます。X-Version secretの検証用overrideは `IWARA_X_VERSION_SECRET`、rate-limit設定は現行helperと同じ `--api-delay` 等を使えます。`login` はC#がtokenを受け取るためstdoutにtokenを返しますが、C#側ではstdout全体をログ出力しません。直接実行時は出力を保存・共有しないでください。

`download` は `.part` / `.part.meta`、ETag、Content-Range、末尾65,536 bytes rewind、atomic finalize、CDN URL再取得を実装しています。外部動画はPythonやpipを使わず、指定されたstandalone `yt-dlp.exe`をRustから起動します。
