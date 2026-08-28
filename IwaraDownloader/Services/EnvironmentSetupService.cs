namespace IwaraDownloader.Services
{
    /// <summary>
    /// Rust helperの配置確認を行う初回セットアップサービス。
    /// Rust helperの実行ファイルを確認し、必要な配置情報だけを記録する。
    /// </summary>
    public sealed class EnvironmentSetupService
    {
        public async Task<string> RunFullSetupAsync(
            string? helperPath,
            string appDir,
            IProgress<SetupProgress>? progress = null,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report(new SetupProgress("Rust helperを確認しています...", 20));

            var resolvedPath = string.IsNullOrWhiteSpace(helperPath)
                ? Path.Combine(appDir, "iwara-helper.exe")
                : (Path.IsPathRooted(helperPath)
                    ? helperPath.Trim('"', ' ')
                    : Path.Combine(appDir, helperPath.Trim('"', ' ')));

            if (!File.Exists(resolvedPath))
                throw new FileNotFoundException(
                    $"Rust helperが見つかりません。iwara-helper.exeをアプリ実行フォルダへ配置してください。",
                    resolvedPath);

            progress?.Report(new SetupProgress("Rust helperの起動ファイルを確認しました。", 70));
            var marker = Path.Combine(appDir, ".rust_setup_done");
            await File.WriteAllTextAsync(
                marker,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\nhelper={resolvedPath}\n",
                ct);
            progress?.Report(new SetupProgress("Rust helperのセットアップが完了しました。", 100));
            return resolvedPath;
        }
    }

    public sealed record SetupProgress(string Message, int Percent);
}
