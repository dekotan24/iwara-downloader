# ソースからビルド

## 必要なもの

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022（推奨）または `dotnet` CLI

## ビルド手順

```powershell
git clone https://github.com/dekotan24/iwara-downloader.git
cd iwara-downloader
dotnet build IwaraDownloader\IwaraDownloader.csproj -c Release
```

出力先: `IwaraDownloader\bin\Release\net8.0-windows\`

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
│   ├── iwara_helper.py   # Python API ヘルパー
│   └── iwara_setup.bat   # セットアップバッチ
├── README.md
├── README_en.md
└── LICENSE
```
