# ソースからビルド

## 必要なもの

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Rust stable toolchain](https://www.rust-lang.org/tools/install)（Rust helperを再ビルドする場合）
- Visual Studio 2022（推奨）または `dotnet` CLI

## ビルド手順

```powershell
git clone https://github.com/dekotan24/iwara-downloader.git
cd iwara-downloader
dotnet build IwaraDownloader\IwaraDownloader.csproj -c Release
```

出力先: `IwaraDownloader\bin\Release\net10.0-windows\`

Rust helperだけを再ビルドする場合:

```powershell
cargo test --manifest-path tools\iwara-rust-poc\Cargo.toml
cargo build --release --manifest-path tools\iwara-rust-poc\Cargo.toml
Copy-Item tools\iwara-rust-poc\target\release\iwara-helper.exe IwaraDownloader\iwara-helper.exe
```

## NuGet パッケージ

| パッケージ | バージョン | 用途 |
|-----------|-----------|------|
| Microsoft.Data.Sqlite | 8.0.0 | SQLite データベース |
| System.Text.Json | 8.0.6 | JSON シリアライズ |
| Microsoft.Extensions.Http | 8.0.0 | HttpClient 管理 |
| System.Security.Cryptography.ProtectedData | 8.0.0 | DPAPI 暗号化 |
| NAudio | 2.2.1 | 効果音再生 |
| TagLibSharp | 2.3.0 | mp4 メタデータタグ |

Web サーバー機能には `Microsoft.AspNetCore.App` フレームワーク参照が使用されています。

## プロジェクト構造

```
iwara-downloader/
├── IwaraDownloader.sln
├── IwaraDownloader/
│   ├── IwaraDownloader.csproj
│   ├── Program.cs
│   ├── Forms/           # WinForms UI
│   ├── Models/           # データモデル
│   ├── Services/         # ビジネスロジック
│   ├── Utils/            # ユーティリティ
│   ├── WebUI/            # Web メディアサーバーのフロントエンド
│   └── iwara-helper.exe  # Rust API / download helper
├── README.md
├── README_en.md
└── LICENSE
```
