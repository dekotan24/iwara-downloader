# iwara-rust-poc

隔離されたRust CLIによる `IwaraDownloader/iwara_helper.py` 移行調査用PoCです。既存C#、Python helper、セットアップ処理は変更しません。

## Build

この環境ではWindows MSVC SDKが不完全だったため、調査時の検証はGNU targetで行いました。通常の利用ではRustの標準Windows toolchainでビルドできます。

```powershell
cargo test
cargo build --release
```

## Commands

```text
iwara-rust-poc login [email] [password]
iwara-rust-poc verify-token [--token TOKEN]
iwara-rust-poc get-video VIDEO_ID [--token TOKEN]
iwara-rust-poc search QUERY [PAGE] [LIMIT] [--token TOKEN]
iwara-rust-poc user-videos USERNAME [--token TOKEN]
iwara-rust-poc get-url VIDEO_ID [QUALITY] [--token TOKEN]
iwara-rust-poc download-test DIRECT_URL [OUTPUT_PATH]
iwara-rust-poc download-test-video VIDEO_ID [QUALITY] [--token TOKEN]
iwara-rust-poc probe URL [--token TOKEN]
```

認証情報は `IWARA_EMAIL`、`IWARA_PASSWORD`、`IWARA_TOKEN`、X-Version secretの検証用overrideは `IWARA_X_VERSION_SECRET` で渡せます。PoCはtoken、Cookie、signed URL、secret値を出力しません。`login` の引数はプロセス一覧に残り得るため、実運用では環境変数を使ってください。
