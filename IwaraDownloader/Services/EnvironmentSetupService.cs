using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Text;

namespace IwaraDownloader.Services
{
    /// <summary>
    /// 初回起動ウィザード用: Python embeddable のDL、解凍、pip / cloudscraper / yt-dlp の自動セットアップ
    /// </summary>
    public class EnvironmentSetupService
    {
        public const string PythonVersion = "3.10.11";
        private static readonly string PythonEmbedUrl =
            $"https://www.python.org/ftp/python/{PythonVersion}/python-{PythonVersion}-embed-amd64.zip";
        private const string GetPipUrl = "https://bootstrap.pypa.io/get-pip.py";

        private static readonly HttpClient _http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd("iwara-downloader/setup");
            return c;
        }

        /// <summary>
        /// Python 自動DL + 解凍 + _pth修正 + pip導入。返り値は python.exe の絶対パス。
        /// </summary>
        public async Task<string> DownloadAndPreparePythonAsync(
            string destDir,
            IProgress<SetupProgress>? progress = null,
            CancellationToken ct = default)
        {
            var tempZip = Path.Combine(Path.GetTempPath(), $"python-{PythonVersion}-embed-amd64.zip");

            progress?.Report(new SetupProgress("Python embeddable をダウンロード中...", 0));

            using (var resp = await _http.GetAsync(PythonEmbedUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                resp.EnsureSuccessStatusCode();
                var total = resp.Content.Headers.ContentLength ?? -1L;
                await using var inStream = await resp.Content.ReadAsStreamAsync(ct);
                await using var outStream = File.Create(tempZip);
                var buf = new byte[81920];
                long readTotal = 0;
                int read;
                while ((read = await inStream.ReadAsync(buf, 0, buf.Length, ct)) > 0)
                {
                    await outStream.WriteAsync(buf, 0, read, ct);
                    readTotal += read;
                    if (total > 0)
                    {
                        int pct = (int)(readTotal * 25 / total);
                        progress?.Report(new SetupProgress(
                            $"Python DL中 {readTotal / 1024 / 1024}MB / {total / 1024 / 1024}MB",
                            pct));
                    }
                }
            }

            progress?.Report(new SetupProgress("Python を展開中...", 30));

            // 既存フォルダがあれば退避
            if (Directory.Exists(destDir))
            {
                var bak = destDir + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                try { Directory.Move(destDir, bak); }
                catch
                {
                    // moveできなければ削除を試みる
                    try { Directory.Delete(destDir, true); } catch { }
                }
            }

            ZipFile.ExtractToDirectory(tempZip, destDir);

            var pythonExe = Path.Combine(destDir, "python.exe");
            if (!File.Exists(pythonExe))
                throw new InvalidOperationException($"Python展開後に python.exe が見つかりません: {pythonExe}");

            progress?.Report(new SetupProgress("Python設定 (._pth) を調整中...", 35));
            await PreparePthAsync(destDir, ct);

            progress?.Report(new SetupProgress("pip をインストール中...", 40));
            await InstallPipAsync(pythonExe, progress, ct);

            return pythonExe;
        }

        /// <summary>
        /// embeddable Python の `python3*._pth` の `#import site` を有効化
        /// </summary>
        public async Task PreparePthAsync(string pythonDir, CancellationToken ct = default)
        {
            foreach (var pth in Directory.GetFiles(pythonDir, "python3*._pth"))
            {
                var lines = await File.ReadAllLinesAsync(pth, ct);
                bool changed = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Trim() == "#import site")
                    {
                        lines[i] = "import site";
                        changed = true;
                    }
                }
                if (changed) await File.WriteAllLinesAsync(pth, lines, ct);
            }
        }

        /// <summary>
        /// pip 導入。既に入っていればスキップ。
        /// </summary>
        public async Task InstallPipAsync(
            string pythonExe,
            IProgress<SetupProgress>? progress = null,
            CancellationToken ct = default)
        {
            // 既に pip があるかチェック
            var check = await RunProcessAsync(pythonExe, "-m pip --version", progress, ct);
            if (check.ExitCode == 0)
            {
                progress?.Report(new SetupProgress("pip は既にインストール済み", 55));
                return;
            }

            var pythonDir = Path.GetDirectoryName(pythonExe)!;
            var getPipPath = Path.Combine(pythonDir, "get-pip.py");

            progress?.Report(new SetupProgress("get-pip.py をダウンロード中...", 45));
            using (var resp = await _http.GetAsync(GetPipUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                resp.EnsureSuccessStatusCode();
                await using var fs = File.Create(getPipPath);
                await resp.Content.CopyToAsync(fs, ct);
            }

            progress?.Report(new SetupProgress("pip をインストール実行中...", 50));
            var result = await RunProcessAsync(
                pythonExe,
                $"\"{getPipPath}\" --no-warn-script-location",
                progress, ct);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"pip インストール失敗 (exit={result.ExitCode})\n{result.Output}");
        }

        /// <summary>
        /// pip パッケージ導入 + import 検証
        /// </summary>
        public async Task InstallPackageAsync(
            string pythonExe,
            string packageName,
            string? importName = null,
            int basePercent = 60,
            IProgress<SetupProgress>? progress = null,
            CancellationToken ct = default)
        {
            progress?.Report(new SetupProgress($"{packageName} をインストール中...", basePercent));
            var inst = await RunProcessAsync(
                pythonExe,
                $"-m pip install -U --no-warn-script-location {packageName}",
                progress, ct);
            if (inst.ExitCode != 0)
                throw new InvalidOperationException($"{packageName} インストール失敗 (exit={inst.ExitCode})\n{inst.Output}");

            importName ??= packageName.Replace("-", "_");
            progress?.Report(new SetupProgress($"{packageName} の動作確認中...", basePercent + 3));
            var verify = await RunProcessAsync(
                pythonExe,
                $"-c \"import {importName}\"",
                progress, ct);
            if (verify.ExitCode != 0)
                throw new InvalidOperationException($"{packageName} import 検証失敗\n{verify.Output}");
        }

        /// <summary>
        /// フルセットアップ。Pythonがnullなら自動DL。完了後にマーカーファイル作成。
        /// 返り値は実際に使用したpython.exeパス。
        /// </summary>
        public async Task<string> RunFullSetupAsync(
            string? pythonPath,
            string appDir,
            IProgress<SetupProgress>? progress = null,
            CancellationToken ct = default)
        {
            string pythonExe;
            if (string.IsNullOrWhiteSpace(pythonPath))
            {
                var destDir = Path.Combine(appDir, "Python310");
                pythonExe = await DownloadAndPreparePythonAsync(destDir, progress, ct);
            }
            else
            {
                pythonExe = pythonPath.Trim('"', ' ');

                // バージョン確認 (pythonとして動くか)
                progress?.Report(new SetupProgress("指定されたPythonを確認中...", 10));
                var ver = await RunProcessAsync(pythonExe, "--version", progress, ct);
                if (ver.ExitCode != 0)
                    throw new InvalidOperationException(
                        $"Pythonの実行に失敗しました: {pythonExe}\n{ver.Output}");

                // embeddable の _pth 修正 (該当ファイルがあれば)
                var pyDir = Path.GetDirectoryName(pythonExe)!;
                await PreparePthAsync(pyDir, ct);

                // pip 確認 / 必要なら導入
                await InstallPipAsync(pythonExe, progress, ct);
            }

            // cloudscraper
            await InstallPackageAsync(pythonExe, "cloudscraper", basePercent: 65, progress: progress, ct: ct);

            // yt-dlp (import名は yt_dlp)
            await InstallPackageAsync(pythonExe, "yt-dlp", importName: "yt_dlp", basePercent: 80, progress: progress, ct: ct);

            // マーカーファイル作成
            progress?.Report(new SetupProgress("セットアップマーカーを作成中...", 95));
            var marker = Path.Combine(appDir, ".python_setup_done");
            await File.WriteAllTextAsync(
                marker,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\npython={pythonExe}\n",
                ct);

            progress?.Report(new SetupProgress("セットアップ完了", 100));
            return pythonExe;
        }

        private async Task<(int ExitCode, string Output)> RunProcessAsync(
            string exe,
            string args,
            IProgress<SetupProgress>? progress = null,
            CancellationToken ct = default)
        {
            var sb = new StringBuilder();
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            using var proc = new Process { StartInfo = psi };
            proc.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    sb.AppendLine(e.Data);
                    progress?.Report(new SetupProgress(e.Data, -1));
                }
            };
            proc.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    sb.AppendLine(e.Data);
                    progress?.Report(new SetupProgress(e.Data, -1));
                }
            };
            proc.Start();
            IwaraDownloader.Utils.ChildProcessJob.AssignProcess(proc); // 親死亡で自動 Kill
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await proc.WaitForExitAsync(ct);
            return (proc.ExitCode, sb.ToString());
        }
    }

    /// <summary>
    /// ウィザード用進捗。Percent が -1 のときはログ追記のみ (バーは更新しない)
    /// </summary>
    public record SetupProgress(string Message, int Percent);
}
