using System.Diagnostics;
using System.Text.Json;
using IwaraDownloader.Models;

namespace IwaraDownloader.Services
{
    /// <summary>
    /// Python iwara_helper.py を呼び出すサービス（Embeddable Python対応）
    /// </summary>
    public class IwaraApiService
    {
        private readonly string _appDir;
        private readonly string _scriptPath;
        private string? _token;

        /// <summary>ログイン済みかどうか</summary>
        public bool IsLoggedIn => !string.IsNullOrEmpty(_token);

        /// <summary>トークン</summary>
        public string? Token => _token;

        /// <summary>Pythonパス（設定から取得）</summary>
        private string PythonPath => Utils.SettingsManager.Instance.Settings.PythonPath;

        /// <summary>Pythonが設定されているか</summary>
        public bool IsPythonConfigured
        {
            get
            {
                var pythonPath = PythonPath;
                if (string.IsNullOrEmpty(pythonPath)) return false;
                // フルパスの場合はファイル存在チェック
                if (Path.IsPathRooted(pythonPath))
                    return File.Exists(pythonPath);
                // "python"などPATH上のコマンドの場合は存在するとみなす
                return true;
            }
        }

        /// <summary>スクリプトが存在するか</summary>
        public bool IsScriptReady => File.Exists(_scriptPath);

        /// <summary>セットアップ完了マーカー</summary>
        private string SetupMarkerPath => Path.Combine(_appDir, ".python_setup_done");

        /// <summary>セットアップ済みか</summary>
        public bool IsSetupDone => File.Exists(SetupMarkerPath);

        public IwaraApiService()
        {
            _appDir = AppDomain.CurrentDomain.BaseDirectory;
            _scriptPath = Path.Combine(_appDir, "iwara_helper.py");
            
            // 保存されたトークンを読み込み
            LoadToken();
            
            // 旧形式のPythonパスファイルがあれば設定に移行
            MigratePythonPath();
        }

        #region Python Path Management

        /// <summary>
        /// Pythonパスを保存（設定に保存）
        /// </summary>
        public void SavePythonPath(string pythonPath)
        {
            var settings = Utils.SettingsManager.Instance.Settings;
            settings.PythonPath = pythonPath;
            Utils.SettingsManager.Instance.Save();
        }

        /// <summary>
        /// 旧形式のPythonパスファイルから設定に移行
        /// </summary>
        private void MigratePythonPath()
        {
            try
            {
                var oldConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "IwaraDownloader",
                    "python_path.txt");
                
                if (File.Exists(oldConfigPath))
                {
                    var pythonPath = File.ReadAllText(oldConfigPath).Trim();
                    if (!string.IsNullOrEmpty(pythonPath))
                    {
                        // 設定がデフォルトの場合のみ移行
                        var settings = Utils.SettingsManager.Instance.Settings;
                        if (settings.PythonPath == "python")
                        {
                            settings.PythonPath = pythonPath;
                            Utils.SettingsManager.Instance.Save();
                            Debug.WriteLine($"Pythonパスを設定に移行しました: {pythonPath}");
                        }
                    }
                    // 旧ファイルを削除
                    File.Delete(oldConfigPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Pythonパス移行エラー: {ex.Message}");
            }
        }

        #endregion

        #region Token Management

        /// <summary>
        /// トークンを保存
        /// </summary>
        private void SaveToken()
        {
            try
            {
                var tokenPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "IwaraDownloader",
                    "token.txt");
                
                var dir = Path.GetDirectoryName(tokenPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                
                if (!string.IsNullOrEmpty(_token))
                    File.WriteAllText(tokenPath, _token);
            }
            catch { }
        }

        /// <summary>
        /// トークンを読み込み
        /// </summary>
        private void LoadToken()
        {
            try
            {
                var tokenPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "IwaraDownloader",
                    "token.txt");
                
                if (File.Exists(tokenPath))
                    _token = File.ReadAllText(tokenPath).Trim();
            }
            catch { }
        }

        #endregion

        /// <summary>
        /// レート制限設定の引数を生成
        /// </summary>
        private List<string> GetRateLimitArgs()
        {
            var settings = Utils.SettingsManager.Instance.Settings;
            return new List<string>
            {
                "--api-delay", (settings.ApiRequestDelayMs / 1000.0).ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--page-delay", (settings.PageFetchDelayMs / 1000.0).ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--rate-limit-base", (settings.RateLimitBaseDelayMs / 1000.0).ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--rate-limit-max", (settings.RateLimitMaxDelayMs / 1000.0).ToString(System.Globalization.CultureInfo.InvariantCulture)
            };
        }

        /// <summary>
        /// Pythonスクリプトを実行
        /// </summary>
        private async Task<JsonDocument?> RunPythonAsync(string action, params string[] args)
        {
            if (!IsPythonConfigured)
            {
                Debug.WriteLine("Python not configured");
                return null;
            }

            if (!IsScriptReady)
            {
                Debug.WriteLine("Script not found");
                return null;
            }

            var allArgs = new List<string> { $"\"{_scriptPath}\"", action };
            allArgs.AddRange(args.Select(a => $"\"{a.Replace("\"", "\\\"")}\"")); 
            
            if (!string.IsNullOrEmpty(_token))
            {
                allArgs.Add("--token");
                allArgs.Add($"\"{_token}\"");
            }

            // レート制限設定を追加
            allArgs.AddRange(GetRateLimitArgs());
            
            // バックオフ無効の場合
            if (!Utils.SettingsManager.Instance.Settings.EnableExponentialBackoff)
            {
                allArgs.Add("--no-backoff");
            }

            var psi = new ProcessStartInfo
            {
                FileName = PythonPath,
                Arguments = string.Join(" ", allArgs),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                WorkingDirectory = _appDir
            };

            Debug.WriteLine($"Running: {PythonPath} {psi.Arguments}");

            using var process = new Process { StartInfo = psi };
            var output = new System.Text.StringBuilder();
            var error = new System.Text.StringBuilder();

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null) output.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    error.AppendLine(e.Data);
                    Debug.WriteLine($"Python stderr: {e.Data}");
                    
                    // LoggingServiceにも出力（エラーレベルの判定）
                    if (e.Data.Contains("Error") || e.Data.Contains("error") || 
                        e.Data.Contains("Exception") || e.Data.Contains("Traceback") ||
                        e.Data.Contains("403") || e.Data.Contains("429"))
                    {
                        LoggingService.Instance.Warn($"Python: {e.Data}");
                    }
                    else if (!e.Data.StartsWith("Progress:"))
                    {
                        LoggingService.Instance.Debug($"Python: {e.Data}");
                    }
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            var outputStr = output.ToString().Trim();
            Debug.WriteLine($"Python output: {outputStr}");

            if (string.IsNullOrEmpty(outputStr))
            {
                var errorStr = error.ToString().Trim();
                Debug.WriteLine($"Python error: {errorStr}");
                if (!string.IsNullOrEmpty(errorStr))
                {
                    LoggingService.Instance.Error($"Pythonスクリプト実行エラー (action={action}):\n{errorStr}");
                }
                return null;
            }

            try
            {
                return JsonDocument.Parse(outputStr);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"JSON parse error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// ログイン
        /// </summary>
        public async Task<(bool Success, string? Error)> LoginAsync(string email, string password)
        {
            var result = await RunPythonAsync("login", email, password);
            
            if (result == null)
                return (false, "Pythonスクリプトの実行に失敗しました。環境セットアップを確認してください。");

            var root = result.RootElement;
            
            if (root.TryGetProperty("success", out var success) && success.GetBoolean())
            {
                if (root.TryGetProperty("token", out var tokenProp))
                {
                    _token = tokenProp.GetString();
                    SaveToken();
                    return (true, null);
                }
            }

            var error = root.TryGetProperty("error", out var errorProp) 
                ? errorProp.GetString() 
                : "Unknown error";
            
            return (false, error);
        }

        /// <summary>
        /// ログアウト
        /// </summary>
        public void Logout()
        {
            _token = null;
            try
            {
                var tokenPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "IwaraDownloader",
                    "token.txt");
                if (File.Exists(tokenPath))
                    File.Delete(tokenPath);
            }
            catch { }
        }

        /// <summary>
        /// ユーザーの動画リストを取得
        /// </summary>
        public async Task<List<VideoInfo>> GetUserVideosAsync(string username, IProgress<string>? progress = null)
        {
            progress?.Report($"{username}の動画一覧を取得中...");

            var result = await RunPythonAsync("get_videos", username);
            
            if (result == null)
            {
                progress?.Report("Pythonスクリプトの実行に失敗しました");
                return new List<VideoInfo>();
            }

            var root = result.RootElement;
            
            if (!root.TryGetProperty("success", out var success) || !success.GetBoolean())
            {
                var error = root.TryGetProperty("error", out var errorProp) 
                    ? errorProp.GetString() 
                    : "Unknown error";
                progress?.Report($"エラー: {error}");
                return new List<VideoInfo>();
            }

            var videos = new List<VideoInfo>();
            
            if (root.TryGetProperty("videos", out var videosArray))
            {
                foreach (var video in videosArray.EnumerateArray())
                {
                    var videoInfo = new VideoInfo
                    {
                        VideoId = video.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                        Title = video.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "",
                        ThumbnailUrl = video.TryGetProperty("thumbnail", out var thumb) ? thumb.GetString() ?? "" : "",
                        DurationSeconds = video.TryGetProperty("duration", out var dur) && dur.ValueKind == JsonValueKind.Number 
                            ? (int)dur.GetDouble() : 0,
                        AuthorUserId = username,
                        AuthorUsername = username
                    };
                    videoInfo.Url = $"https://www.iwara.tv/video/{videoInfo.VideoId}";
                    videos.Add(videoInfo);
                }
            }

            var count = root.TryGetProperty("count", out var countProp) ? countProp.GetInt32() : videos.Count;
            progress?.Report($"{count}件の動画を取得しました");

            return videos;
        }

        /// <summary>
        /// ダウンロードURLを取得
        /// </summary>
        public async Task<(bool Success, string? Url, string? Quality, string? Title, string? Error)> GetDownloadUrlAsync(string videoId)
        {
            var result = await RunPythonAsync("get_url", videoId);
            
            if (result == null)
                return (false, null, null, null, "Pythonスクリプトの実行に失敗しました");

            var root = result.RootElement;
            
            if (root.TryGetProperty("success", out var success) && success.GetBoolean())
            {
                var url = root.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
                var quality = root.TryGetProperty("quality", out var qualityProp) ? qualityProp.GetString() : null;
                var title = root.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null;
                return (true, url, quality, title, null);
            }

            var error = root.TryGetProperty("error", out var errorProp) 
                ? errorProp.GetString() 
                : "Unknown error";
            
            return (false, null, null, null, error);
        }

        /// <summary>
        /// 動画をダウンロード（Pythonに任せる）
        /// </summary>
        public async Task<(bool Success, string? Error)> DownloadVideoAsync(
            string videoId, 
            string outputPath,
            IProgress<string>? progress = null,
            IProgress<double>? percentProgress = null)
        {
            progress?.Report($"ダウンロード中: {videoId}");

            var result = await RunPythonWithProgressAsync("download", percentProgress, videoId, outputPath);
            
            if (result == null)
                return (false, "Pythonスクリプトの実行に失敗しました");

            var root = result.RootElement;
            
            if (root.TryGetProperty("success", out var success) && success.GetBoolean())
            {
                progress?.Report("ダウンロード完了");
                return (true, null);
            }

            var error = root.TryGetProperty("error", out var errorProp) 
                ? errorProp.GetString() 
                : "Unknown error";
            
            return (false, error);
        }

        /// <summary>
        /// Pythonスクリプトを実行（進捗リアルタイム取得）
        /// </summary>
        private async Task<JsonDocument?> RunPythonWithProgressAsync(string action, IProgress<double>? percentProgress, params string[] args)
        {
            if (!IsPythonConfigured)
            {
                Debug.WriteLine("Python not configured");
                return null;
            }

            if (!IsScriptReady)
            {
                Debug.WriteLine("Script not found");
                return null;
            }

            var allArgs = new List<string> { $"\"{_scriptPath}\"", action };
            allArgs.AddRange(args.Select(a => $"\"{a.Replace("\"", "\\\"")}\""));
            
            if (!string.IsNullOrEmpty(_token))
            {
                allArgs.Add("--token");
                allArgs.Add($"\"{_token}\"");
            }

            var psi = new ProcessStartInfo
            {
                FileName = PythonPath,
                Arguments = string.Join(" ", allArgs),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                WorkingDirectory = _appDir
            };

            using var process = new Process { StartInfo = psi };
            var output = new System.Text.StringBuilder();

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null) output.AppendLine(e.Data);
            };

            // stderrから進捗をリアルタイム取得
            var errorOutput = new System.Text.StringBuilder();
            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    errorOutput.AppendLine(e.Data);
                    Debug.WriteLine($"Python stderr: {e.Data}");
                    
                    // Progress: XX.X% 形式をパース
                    if (e.Data.StartsWith("Progress:") && percentProgress != null)
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(e.Data, @"Progress:\s*([\d.]+)%");
                        if (match.Success && double.TryParse(match.Groups[1].Value, out var pct))
                        {
                            percentProgress.Report(pct);
                        }
                    }
                    // LoggingServiceにも出力（エラーレベルの判定）
                    else if (e.Data.Contains("Error") || e.Data.Contains("error") || 
                             e.Data.Contains("Exception") || e.Data.Contains("Traceback") ||
                             e.Data.Contains("403") || e.Data.Contains("429"))
                    {
                        LoggingService.Instance.Warn($"Python: {e.Data}");
                    }
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            var outputStr = output.ToString().Trim();

            if (string.IsNullOrEmpty(outputStr))
            {
                var errorStr = errorOutput.ToString().Trim();
                if (!string.IsNullOrEmpty(errorStr))
                {
                    LoggingService.Instance.Error($"Pythonスクリプト実行エラー (action={action}):\n{errorStr}");
                }
                return null;
            }

            try
            {
                return JsonDocument.Parse(outputStr);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"JSON parse error: {ex.Message}");
                LoggingService.Instance.Error($"Python出力JSONパースエラー: {ex.Message}\nOutput: {outputStr}");
                return null;
            }
        }

        /// <summary>
        /// セットアップバッチを実行
        /// </summary>
        public async Task<bool> RunSetupAsync(string pythonPath, IProgress<string>? progress = null)
        {
            var setupBat = Path.Combine(_appDir, "iwara_setup.bat");
            
            if (!File.Exists(setupBat))
            {
                progress?.Report("iwara_setup.batが見つかりません");
                return false;
            }

            // Pythonパスを保存
            SavePythonPath(pythonPath);

            // 古いマーカーファイルを削除（再セットアップ対応）
            if (File.Exists(SetupMarkerPath))
            {
                try { File.Delete(SetupMarkerPath); } catch { }
            }

            progress?.Report("セットアップを実行中...");

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{setupBat}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = false, // セットアップ中は表示
                WorkingDirectory = _appDir
            };

            // 環境変数を設定
            psi.Environment["PYTHON_PATH"] = pythonPath;

            using var process = new Process { StartInfo = psi };

            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Debug.WriteLine($"Setup: {e.Data}");
                    progress?.Report(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            
            await process.WaitForExitAsync();

            // プロセス終了後、少し待ってからマーカーファイルを確認
            await Task.Delay(500);

            var setupSuccess = process.ExitCode == 0 && File.Exists(SetupMarkerPath);
            
            if (!setupSuccess && process.ExitCode == 0)
            {
                // ExitCode=0だがマーカーがない場合、少し待ってリトライ
                await Task.Delay(1000);
                setupSuccess = File.Exists(SetupMarkerPath);
            }

            progress?.Report(setupSuccess ? "セットアップ完了" : "セットアップに失敗しました");
            
            return setupSuccess;
        }

        /// <summary>
        /// 環境チェック
        /// </summary>
        public (bool PythonReady, bool ScriptReady) CheckEnvironment()
        {
            return (IsPythonConfigured && IsSetupDone, IsScriptReady);
        }
    }
}
