# ソースからビルド

## 必要なもの

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Visual Studio 2022（推奨）または `dotnet` CLI

## ビルド手順

```powershell
git clone https://github.com/dekotan24/iwara-downloader.git
cd iwara-downloader
dotnet build IwaraDownloader.sln -c Release
```

ソリューションには次の 2 つのプロジェクトが含まれます。

| プロジェクト | 成果物 | 内容 |
|-------------|--------|------|
| IwaraDownloader | `IwaraDownloader.exe` | アプリ本体 |
| IwaraDownloader.DbTool | `DBMaintenanceTool.exe` | [DB 操作ツール](Database-Tool) |

出力先はどちらも `IwaraDownloader\bin\Release\net10.0-windows\` です。本体がツールメニューから `DBMaintenanceTool.exe` を同じフォルダ内で探すため、DbTool 側の `OutputPath` を本体と同じ場所に向けてあります。

## NuGet パッケージ

| パッケージ | バージョン | 用途 |
|-----------|-----------|------|
| Microsoft.Data.Sqlite | 10.0.10 | SQLite データベース |
| SQLitePCLRaw.bundle_e_sqlite3 | 2.1.12 | SQLite ネイティブバイナリ。間接依存の古いバージョンに既知の脆弱性があるため明示的に固定 |
| CommunityToolkit.Mvvm | 8.4.0 | MVVM（ObservableProperty / RelayCommand） |
| VirtualizingWrapPanel | 2.5.1 | タイル表示の仮想化 |
| NAudio | 2.2.1 | 効果音再生 |
| TagLibSharp | 2.3.0 | mp4 メタデータタグ |

Web サーバー機能には `Microsoft.AspNetCore.App` のフレームワーク参照を使用しています。

## プロジェクト構造

```
iwara-downloader/
├── IwaraDownloader.sln
├── IwaraDownloader/
│   ├── IwaraDownloader.csproj
│   ├── Program.cs
│   ├── Wpf/              # WPF UI (Views / ViewModels / Themes / Markup)
│   ├── Forms/            # 設定画面など、WinForms のまま残している画面
│   ├── Models/           # データモデル
│   ├── Services/         # ビジネスロジック
│   ├── Utils/            # ユーティリティ（L / Localizer / SettingsManager 等）
│   ├── Resources/        # 多言語リソース (Strings.resx / .en / .zh-Hans)
│   ├── WebUI/            # Web メディアサーバーのフロントエンド
│   ├── iwara_helper.py   # Python API ヘルパー
│   └── iwara_setup.bat   # セットアップバッチ
├── IwaraDownloader.DbTool/   # DB 操作ツール（本体とソースをリンク共有）
├── tools/                    # 開発用スクリプト
├── wiki/                     # このドキュメント
├── README.md
├── README_en.md
└── LICENSE
```

`UseWPF` と `UseWindowsForms` を併用しています。UI の主体は WPF ですが、設定画面などが WinForms のまま残っており、タスクトレイアイコンにも WinForms の `NotifyIcon` を使っているためです。

## DB 操作ツールのプロジェクト構成

`IwaraDownloader.DbTool` は本体とは別プロセスの独立した実行ファイルですが、DB スキーマやマイグレーションを二重管理しないよう、`DatabaseService` などのソースを本体から**リンク参照**しています（コピーではありません）。

多言語リソースも本体の `Strings*.resx` をリンクして埋め込んでいます。このため `RootNamespace` は本体と同じ `IwaraDownloader` に揃える必要があります（`AssemblyName` は `DBMaintenanceTool` ですが別設定なので影響しません）。

## 多言語リソース

文言はすべて `IwaraDownloader/Resources/Strings.resx`（日本語＝ニュートラル）と、`Strings.en.resx` / `Strings.zh-Hans.resx` のサテライトアセンブリで管理します。

- コード内からは `L.T("キー")` で取得します。キーが存在しない場合はキー文字列がそのまま返るため、追加漏れは画面にキー名として現れます
- WinForms のフォームは `Localizer.Apply(this)` が `{フォーム名}_{フィールド名}` の規約でコントロールの文言を一括適用します。リソースにキーが無いコントロールは Designer 既定（日本語）のままになります

文言を追加・変更したときは 3 言語すべてに同じキーを追加してください。
