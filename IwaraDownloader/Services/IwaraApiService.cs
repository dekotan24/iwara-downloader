using System.Diagnostics;
using System.Text.Json;
using IwaraDownloader.Models;

namespace IwaraDownloader.Services
{
    /// <summary>
    /// Python iwara_helper.py を呼び出すサービス(Embeddable Python対応)
    /// </summary>
    public class IwaraApiService
    {
        private readonly string _appDir;
        private readonly string _scriptPath;
        private string? _token;

        /// <summary>トークン(JWT)を保持しており、かつ JWT の exp が有効期限内である</summary>
        public bool IsLoggedIn => !string.IsNullOrEmpty(_token) && !IsTokenExpired(_token);

        /// <summary>トークンの有効期限(UTC)。無効なら null</summary>
        public DateTime? TokenExpiresAt => string.IsNullOrEmpty(_token) ? null : GetJwtExpiration(_token);

        /// <summary>トークン</summary>
        public string? Token => _token;

        /// <summary>Pythonパス(設定から取得)</summary>
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
        /// Pythonパスを保存(設定に保存)
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
        /// トークンを読み込み。JWT の有効期限をチェックし、期限切れなら破棄する。
        /// </summary>
        private void LoadToken()
        {
            try
            {
                var tokenPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "IwaraDownloader",
                    "token.txt");

                if (!File.Exists(tokenPath)) return;

                var token = File.ReadAllText(tokenPath).Trim();
                if (string.IsNullOrEmpty(token)) return;

                if (IsTokenExpired(token))
                {
                    LoggingService.Instance.Warn("保存されていたトークンの有効期限が切れていたため破棄しました。再ログインが必要です。");
                    try { File.Delete(tokenPath); } catch { }
                    return;
                }

                _token = token;
            }
            catch { }
        }

        /// <summary>
        /// JWT の exp クレームをデコードして有効期限 (UTC) を取得する。失敗時は null
        /// </summary>
        private static DateTime? GetJwtExpiration(string token)
        {
            try
            {
                var parts = token.Split('.');
                if (parts.Length != 3) return null;

                var payloadB64 = parts[1]
                    .Replace('-', '+').Replace('_', '/')
                    .PadRight(parts[1].Length + (4 - parts[1].Length % 4) % 4, '=');
                var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payloadB64));
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("exp", out var expProp)) return null;
                if (!expProp.TryGetInt64(out var exp)) return null;
                return DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// JWT の有効期限が切れているか判定 (60秒のleewayあり)
        /// </summary>
        private static bool IsTokenExpired(string token)
        {
            var exp = GetJwtExpiration(token);
            if (exp == null) return false; // exp を持たないトークンは期限なし扱い
            return DateTime.UtcNow >= exp.Value - TimeSpan.FromSeconds(60);
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
        /// Pythonスクリプトを実行 (site 指定可)
        /// </summary>
        private Task<JsonDocument?> RunPythonAsync(string action, params string[] args)
            => RunPythonAsync(action, null, args);

        private async Task<JsonDocument?> RunPythonAsync(string action, string? site, params string[] args)
        {
            if (!IsPythonConfigured)
            {
                var msg = $"Pythonが設定されていません (PythonPath=\"{PythonPath}\")。設定画面で正しいPythonパスを指定してください。" +
                          " インストール直後の場合、PATHを反映するためにPCの再起動が必要なことがあります。";
                Debug.WriteLine(msg);
                LoggingService.Instance.Error($"[Python実行] {msg} (action={action})");
                return null;
            }

            if (!IsScriptReady)
            {
                var msg = $"iwara_helper.py が見つかりません ({_scriptPath})。インストールが破損している可能性があります。";
                Debug.WriteLine(msg);
                LoggingService.Instance.Error($"[Python実行] {msg} (action={action})");
                return null;
            }

            if (!IsSetupDone)
            {
                LoggingService.Instance.Warn($"[Python実行] セットアップマーカーが見つかりません (.python_setup_done)。" +
                    "ライブラリ未インストールの可能性があります。設定画面から再セットアップを実行してください。 (action={action})");
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

            // iwara.ai / iwara.tv 切替 (空なら省略=デフォルト www.iwara.tv)
            if (!string.IsNullOrEmpty(site))
            {
                allArgs.Add("--site");
                allArgs.Add($"\"{site}\"");
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
                    
                    // LoggingServiceにも出力(エラーレベルの判定)
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
        /// サーバーにトークンを問い合わせて有効性を確認する。起動時や長期アイドル復帰後に呼ぶ。
        /// 期限切れ・サーバー拒否が判明した場合は内部トークンを破棄する。
        /// </summary>
        public async Task<(bool Valid, string? Error)> VerifyTokenAsync()
        {
            if (string.IsNullOrEmpty(_token))
                return (false, "未ログインです");

            if (IsTokenExpired(_token))
            {
                LoggingService.Instance.Warn("トークンの有効期限が切れています。再ログインが必要です。");
                Logout();
                return (false, "トークンの有効期限が切れています");
            }

            var result = await RunPythonAsync("verify_token");
            if (result == null)
                return (false, "トークンの検証に失敗しました(Python実行エラー)");

            var root = result.RootElement;
            if (root.TryGetProperty("success", out var success) && success.GetBoolean())
                return (true, null);

            var code = root.TryGetProperty("code", out var codeProp) ? codeProp.GetString() : null;
            var error = root.TryGetProperty("error", out var errorProp) ? errorProp.GetString() : "Unknown error";

            // サーバー側で明確に無効と判定されたらトークンを破棄
            if (code is "TOKEN_EXPIRED" or "TOKEN_INVALID" or "LOGIN_REQUIRED")
            {
                LoggingService.Instance.Warn($"トークンがサーバーに拒否されました ({code})。ログアウトします。");
                Logout();
            }

            return (false, error);
        }

        /// <summary>
        /// ユーザーの動画リストを取得 (site で iwara.tv / iwara.ai 切替)
        /// </summary>
        public async Task<List<VideoInfo>> GetUserVideosAsync(string username, IProgress<string>? progress = null, string? site = null)
        {
            if (!IsLoggedIn)
            {
                progress?.Report("ログインが必要です。設定画面からログインしてください。");
                return new List<VideoInfo>();
            }

            progress?.Report($"{username}の動画一覧を取得中...");

            var result = await RunPythonAsync("get_videos", site, username);
            
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
                        EmbedUrl = video.TryGetProperty("embed_url", out var embed) ? embed.GetString() ?? "" : "",
                        Rating = video.TryGetProperty("rating", out var rt) ? rt.GetString() ?? "" : "",
                        Site = site ?? Utils.Helpers.SiteTv,
                        AuthorUserId = username,
                        AuthorUsername = username
                    };
                    // 動画 URL は site に応じて組み立て (iwara.tv / iwara.ai)
                    var siteHost = string.IsNullOrEmpty(videoInfo.Site) ? Utils.Helpers.SiteTv : videoInfo.Site;
                    videoInfo.Url = $"https://{siteHost}/video/{videoInfo.VideoId}";
                    videos.Add(videoInfo);
                }
            }

            var count = root.TryGetProperty("count", out var countProp) ? countProp.GetInt32() : videos.Count;
            progress?.Report($"{count}件の動画を取得しました");

            return videos;
        }

        /// <summary>
        /// ダウンロードURLを取得 (file_id / author 情報込み, site で iwara.tv / iwara.ai 切替)。
        /// site 未指定で iwara.tv で叩いて "errors.differentSite" が返った場合は自動で iwara.ai を再試行する
        /// (ローカルファイルから逆引きする ImportFromFolderWizard 等で site が不明なケース向け)。
        /// </summary>
        public async Task<VideoUrlInfo> GetDownloadUrlAsync(string videoId, string? site = null)
        {
            if (!IsLoggedIn)
                return VideoUrlInfo.FromError("ログインが必要です。設定画面からログインしてください。");

            var info = await GetDownloadUrlInternalAsync(videoId, site);
            // site が未指定 (= iwara.tv デフォルト) かつ "errors.differentSite" → iwara.ai で再試行
            if (!info.Success
                && string.IsNullOrEmpty(site)
                && (info.Error?.Contains("differentSite", StringComparison.OrdinalIgnoreCase) ?? false))
            {
                Debug.WriteLine($"GetDownloadUrl: differentSite detected for {videoId}, retrying with www.iwara.ai");
                var retry = await GetDownloadUrlInternalAsync(videoId, Utils.Helpers.SiteAi);
                if (retry.Success)
                {
                    // 呼び出し側に site を伝えるため Rating の隣に既存フィールドを使う...のは美しくないので
                    // 専用プロパティ ResolvedSite を VideoUrlInfo に追加
                    retry.ResolvedSite = Utils.Helpers.SiteAi;
                    return retry;
                }
            }
            return info;
        }

        private async Task<VideoUrlInfo> GetDownloadUrlInternalAsync(string videoId, string? site)
        {
            var result = await RunPythonAsync("get_url", site, videoId);

            if (result == null)
                return VideoUrlInfo.FromError("Pythonスクリプトの実行に失敗しました");

            var root = result.RootElement;

            if (root.TryGetProperty("success", out var success) && success.GetBoolean())
            {
                return new VideoUrlInfo
                {
                    Success = true,
                    Url = GetString(root, "url"),
                    Quality = GetString(root, "quality"),
                    Title = GetString(root, "title"),
                    FileUuid = GetString(root, "file_id"),
                    AuthorUsername = GetString(root, "author_username"),
                    AuthorName = GetString(root, "author_name"),
                    Rating = GetString(root, "rating"),
                };
            }

            return VideoUrlInfo.FromError(GetString(root, "error") ?? "Unknown error");
        }

        private static string? GetString(JsonElement root, string name)
            => root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

        /// <summary>
        /// GetDownloadUrlAsync の戻り値
        /// </summary>
        public class VideoUrlInfo
        {
            public bool Success { get; set; }
            public string? Url { get; set; }
            public string? Quality { get; set; }
            public string? Title { get; set; }
            public string? FileUuid { get; set; }
            public string? AuthorUsername { get; set; }
            public string? AuthorName { get; set; }
            public string? Rating { get; set; }
            public string? Error { get; set; }

            /// <summary>
            /// 自動 site フォールバックで成功した時にどちらの site で取れたかを返す。
            /// 呼び出し側 (DownloadManager.MigrateExistingFiles 等) で DB の Site カラムに反映する。
            /// 通常リクエストで成功した時は null。
            /// </summary>
            public string? ResolvedSite { get; set; }

            public static VideoUrlInfo FromError(string error) => new() { Success = false, Error = error };
        }

        /// <summary>
        /// 動画をダウンロード(Pythonに任せる、site で iwara.tv / iwara.ai 切替)
        /// </summary>
        public async Task<(bool Success, string? Error)> DownloadVideoAsync(
            string videoId,
            string outputPath,
            IProgress<string>? progress = null,
            IProgress<double>? percentProgress = null,
            CancellationToken ct = default,
            string? site = null)
        {
            if (!IsLoggedIn)
                return (false, "ログインが必要です。設定画面からログインしてください。");

            progress?.Report($"ダウンロード中: {videoId}");

            var result = await RunPythonWithProgressAsync("download", percentProgress, ct, site, videoId, outputPath);
            
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
        /// yt-dlp で外部動画(YouTube埋め込み等)をダウンロード
        /// </summary>
        public async Task<(bool Success, string? Error, string? FilePath)> DownloadExternalVideoAsync(
            string embedUrl,
            string outputPath,
            IProgress<string>? progress = null,
            IProgress<double>? percentProgress = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(embedUrl))
                return (false, "埋め込みURLが空です", null);

            progress?.Report($"外部動画DL中: {embedUrl}");

            var ytDlpPath = Utils.SettingsManager.Instance.Settings.YtDlpPath;
            if (string.IsNullOrWhiteSpace(ytDlpPath))
                ytDlpPath = "yt-dlp";

            var result = await RunPythonWithProgressAsync(
                "download_external",
                percentProgress,
                ct,
                embedUrl,
                outputPath,
                "--yt-dlp-path",
                ytDlpPath);

            if (result == null)
                return (false, "Pythonスクリプトの実行に失敗しました", null);

            var root = result.RootElement;

            if (root.TryGetProperty("success", out var success) && success.GetBoolean())
            {
                var filePath = root.TryGetProperty("file_path", out var fpProp) ? fpProp.GetString() : null;
                progress?.Report("外部動画DL完了");
                return (true, null, filePath);
            }

            var error = root.TryGetProperty("error", out var errorProp)
                ? errorProp.GetString()
                : "Unknown error";

            return (false, error, null);
        }

        /// <summary>
        /// Pythonスクリプトを実行(進捗リアルタイム取得、site 指定可)
        /// </summary>
        private Task<JsonDocument?> RunPythonWithProgressAsync(string action, IProgress<double>? percentProgress, CancellationToken ct, params string[] args)
            => RunPythonWithProgressAsync(action, percentProgress, ct, null, args);

        private async Task<JsonDocument?> RunPythonWithProgressAsync(string action, IProgress<double>? percentProgress, CancellationToken ct, string? site, params string[] args)
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

            if (!string.IsNullOrEmpty(site))
            {
                allArgs.Add("--site");
                allArgs.Add($"\"{site}\"");
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
                    // LoggingServiceにも出力(エラーレベルの判定)
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

            try
            {
                await process.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                // アプリ終了/タスクキャンセル: yt-dlp/ffmpeg を含むプロセスツリーを Kill
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);

                    // Kill は非同期完了なので、ゾンビ化防止のため終了確定まで短時間待機
                    using var killWaitCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    try
                    {
                        await process.WaitForExitAsync(killWaitCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.WriteLine("Python process did not exit within 5s after Kill");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Python process Kill failed: {ex.Message}");
                }
                throw;
            }

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

            LoggingService.Instance.Info($"[セットアップ開始] pythonPath={pythonPath}, setupBat={setupBat}, appDir={_appDir}");

            if (!File.Exists(setupBat))
            {
                var msg = $"iwara_setup.batが見つかりません ({setupBat})";
                progress?.Report(msg);
                LoggingService.Instance.Error($"[セットアップ] {msg}");
                return false;
            }

            // Pythonパスを保存
            SavePythonPath(pythonPath);
            LoggingService.Instance.Info($"[セットアップ] Pythonパスを保存: {pythonPath}");

            // 事前にPython自体の起動を試行(PATH 反映漏れの早期検出)
            try
            {
                var checkPsi = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                using var checkProc = Process.Start(checkPsi);
                if (checkProc != null)
                {
                    await checkProc.WaitForExitAsync();
                    var versionOut = (await checkProc.StandardOutput.ReadToEndAsync()).Trim();
                    var versionErr = (await checkProc.StandardError.ReadToEndAsync()).Trim();
                    if (checkProc.ExitCode == 0)
                    {
                        LoggingService.Instance.Info($"[セットアップ] Pythonバージョン確認OK: {versionOut} {versionErr}");
                    }
                    else
                    {
                        LoggingService.Instance.Warn($"[セットアップ] Python --version 失敗 (exit={checkProc.ExitCode}): out={versionOut} err={versionErr}");
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error($"[セットアップ] Python起動チェック失敗: {ex.Message}。PATH反映のためPC再起動が必要かも。{ex.GetType().Name}");
            }

            // 古いマーカーファイルを削除(再セットアップ対応)
            if (File.Exists(SetupMarkerPath))
            {
                try { File.Delete(SetupMarkerPath); LoggingService.Instance.Info("[セットアップ] 既存マーカー削除"); } catch (Exception ex) { LoggingService.Instance.Warn($"[セットアップ] マーカー削除失敗: {ex.Message}"); }
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

            var setupOutput = new System.Text.StringBuilder();
            var setupError = new System.Text.StringBuilder();

            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Debug.WriteLine($"Setup: {e.Data}");
                    setupOutput.AppendLine(e.Data);
                    LoggingService.Instance.Info($"[セットアップ stdout] {e.Data}");
                    progress?.Report(e.Data);
                }
            };
            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Debug.WriteLine($"Setup stderr: {e.Data}");
                    setupError.AppendLine(e.Data);
                    LoggingService.Instance.Warn($"[セットアップ stderr] {e.Data}");
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            LoggingService.Instance.Info($"[セットアップ] プロセス終了 ExitCode={process.ExitCode}");

            // プロセス終了後、少し待ってからマーカーファイルを確認
            await Task.Delay(500);

            var setupSuccess = process.ExitCode == 0 && File.Exists(SetupMarkerPath);
            
            if (!setupSuccess && process.ExitCode == 0)
            {
                // ExitCode=0だがマーカーがない場合、少し待ってリトライ
                await Task.Delay(1000);
                setupSuccess = File.Exists(SetupMarkerPath);
            }

            if (setupSuccess)
            {
                LoggingService.Instance.Info("[セットアップ] 成功");
                progress?.Report("セットアップ完了");
            }
            else
            {
                var errSummary = setupError.ToString().Trim();
                var outSummary = setupOutput.ToString().Trim();
                LoggingService.Instance.Error(
                    $"[セットアップ] 失敗 ExitCode={process.ExitCode}, マーカー={File.Exists(SetupMarkerPath)}, " +
                    $"PathHint='Pythonが新規インストール直後ならPC再起動でPATHを反映してください'\n" +
                    $"--- stderr ---\n{errSummary}\n--- stdout(末尾) ---\n{(outSummary.Length > 1500 ? outSummary[^1500..] : outSummary)}");
                progress?.Report("セットアップに失敗しました(詳細はログ参照)");
            }

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
