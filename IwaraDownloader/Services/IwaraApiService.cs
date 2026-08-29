using IwaraDownloader.Utils;
using System.Diagnostics;
using System.Text.Json;
using IwaraDownloader.Models;

namespace IwaraDownloader.Services
{
    /// <summary>
    /// チャンネル動画一覧取得の結果種別。Success/UserNotFound は確定した状態、Failed は
    /// ログイン未済・レート制限・一時的な通信エラー等の「今回は分からなかった」を表す。
    /// 呼び出し側は Failed のとき、それ以前に確定していたアカウント消滅フラグ等を変更してはいけない。
    /// </summary>
    public enum ChannelFetchStatus
    {
        Success,
        UserNotFound,
        Failed,
    }

    /// <summary>
    /// 同梱Rust iwara-helper.exeを呼び出すサービス。stdout JSON / stderr進捗契約を維持する。
    /// </summary>
    public class IwaraApiService
    {
        private readonly string _appDir;
        private readonly string _rustHelperPath;
        private string? _token;

        /// <summary>トークン(JWT)を保持しており、かつ JWT の exp が有効期限内である</summary>
        public bool IsLoggedIn => !string.IsNullOrEmpty(_token) && !IsTokenExpired(_token);

        /// <summary>トークンの有効期限(UTC)。無効なら null</summary>
        public DateTime? TokenExpiresAt => string.IsNullOrEmpty(_token) ? null : GetJwtExpiration(_token);

        /// <summary>トークン</summary>
        public string? Token => _token;

        /// <summary>Rust helperの実行パス。設定値が無効なら同梱exeを使う。</summary>
        private string RustHelperPath
        {
            get
            {
                var configured = Utils.SettingsManager.Instance.Settings.RustHelperPath;
                if (!string.IsNullOrWhiteSpace(configured))
                {
                    var path = Path.IsPathRooted(configured)
                        ? configured
                        : Path.Combine(_appDir, configured);
                    if (File.Exists(path)) return path;
                }
                return _rustHelperPath;
            }
        }

        /// <summary>Rust helperが設定されているか</summary>
        public bool IsRustConfigured => File.Exists(RustHelperPath);

        /// <summary>Rust helperが存在するか</summary>
        public bool IsScriptReady => IsRustConfigured;

        /// <summary>セットアップ完了マーカー</summary>
        private string SetupMarkerPath => Path.Combine(_appDir, ".rust_setup_done");

        /// <summary>セットアップ済みか</summary>
        public bool IsSetupDone => IsRustConfigured;

        public IwaraApiService()
        {
            _appDir = AppDomain.CurrentDomain.BaseDirectory;
            _rustHelperPath = Path.Combine(_appDir, "iwara-helper.exe");
            
            // 保存されたトークンを読み込み
            LoadToken();
            
            // Rust移行後は旧Pythonパス設定を参照しない。
        }

        #region Rust helper Path Management

        /// <summary>
        /// Rust helperパスを保存(設定に保存)
        /// </summary>
        public void SaveRustHelperPath(string helperPath)
        {
            var settings = Utils.SettingsManager.Instance.Settings;
            settings.RustHelperPath = helperPath;
            Utils.SettingsManager.Instance.Save();
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
        /// Rust helperを実行 (site 指定可)
        /// </summary>
        private Task<JsonDocument?> RunRustAsync(string action, params string[] args)
            => RunRustAsync(action, null, CancellationToken.None, args);

        private Task<JsonDocument?> RunRustAsync(string action, string? site, params string[] args)
            => RunRustAsync(action, site, CancellationToken.None, args);

        private Task<JsonDocument?> RunRustAsync(string action, string? site, CancellationToken ct, params string[] args)
            => RunRustProcessAsync(action, site, ct, null, args);

        private Task<JsonDocument?> RunRustWithEnvironmentAsync(
            string action,
            IReadOnlyDictionary<string, string> environment,
            params string[] args)
            => RunRustProcessAsync(action, null, CancellationToken.None, environment, args);

        private async Task<JsonDocument?> RunRustProcessAsync(
            string action,
            string? site,
            CancellationToken ct,
            IReadOnlyDictionary<string, string>? environment,
            params string[] args)
        {
            if (!IsRustConfigured)
            {
                var msg = $"Rust helperが見つかりません ({RustHelperPath})。アプリのインストールが破損している可能性があります。";
                Debug.WriteLine(msg);
                LoggingService.Instance.Error($"[Rust実行] {msg} (action={action})");
                return null;
            }

            var allArgs = new List<string> { action };
            allArgs.AddRange(args);

            // トークンは環境変数 IWARA_TOKEN 経由で渡す。Rust helperはこの値をログに出さない。

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
                allArgs.Add(site);
            }

            var psi = new ProcessStartInfo
            {
                FileName = RustHelperPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
                WorkingDirectory = _appDir
            };
            foreach (var argument in allArgs)
                psi.ArgumentList.Add(argument);
            if (!string.IsNullOrEmpty(_token))
            {
                psi.EnvironmentVariables["IWARA_TOKEN"] = _token;
            }
            if (environment != null)
            {
                foreach (var pair in environment)
                    psi.EnvironmentVariables[pair.Key] = pair.Value;
            }

            Debug.WriteLine($"Running Rust helper: {RustHelperPath} {action}");

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
                    Debug.WriteLine($"Rust stderr: {e.Data}");
                    
                    // LoggingServiceにも出力(エラーレベルの判定)
                    if (e.Data.Contains("Error") || e.Data.Contains("error") || 
                        e.Data.Contains("Exception") || e.Data.Contains("Traceback") ||
                        e.Data.Contains("403") || e.Data.Contains("429"))
                    {
                        LoggingService.Instance.Warn($"Rust: {e.Data}");
                    }
                    else if (!e.Data.StartsWith("Progress:"))
                    {
                        LoggingService.Instance.Debug($"Rust: {e.Data}");
                    }
                }
            };

            process.Start();
            Utils.ChildProcessJob.AssignProcess(process); // 親死亡で自動 Kill
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await process.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
            // タイムアウト/アプリ終了: Rust helperとその子プロセスをKill
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
                        Debug.WriteLine("Rust helper did not exit within 5s after Kill");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Rust helper Kill failed: {ex.Message}");
                }
                throw;
            }

            var outputStr = output.ToString().Trim();
            Debug.WriteLine($"Rust output received ({outputStr.Length} chars)");

            if (string.IsNullOrEmpty(outputStr))
            {
                var errorStr = error.ToString().Trim();
                Debug.WriteLine($"Rust error: {errorStr}");
                if (!string.IsNullOrEmpty(errorStr))
                {
                    LoggingService.Instance.Error($"Rust helper実行エラー (action={action}):\n{errorStr}");
                }
                return null;
            }

            try
            {
                return JsonDocument.Parse(outputStr);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Rust JSON parse error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// ログイン
        /// </summary>
        public async Task<(bool Success, string? Error)> LoginAsync(string email, string password)
        {
            var result = await RunRustWithEnvironmentAsync(
                "login",
                new Dictionary<string, string>
                {
                    ["IWARA_EMAIL"] = email,
                    ["IWARA_PASSWORD"] = password,
                });
            
            if (result == null)
                return (false, "Rust helperの実行に失敗しました。アプリの配置を確認してください。");

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

            var result = await RunRustAsync("verify_token");
            if (result == null)
                return (false, "トークンの検証に失敗しました(Rust helper実行エラー)");

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

        /// <summary>検索APIのJSONをRust helperから取得する。</summary>
        public Task<JsonDocument?> SearchVideosAsync(
            string query,
            int page = 0,
            int limit = 32,
            string? site = null,
            CancellationToken ct = default)
            => RunRustAsync(
                "search",
                site,
                ct,
                query,
                page.ToString(System.Globalization.CultureInfo.InvariantCulture),
                limit.ToString(System.Globalization.CultureInfo.InvariantCulture));

        /// <summary>
        /// ユーザーの動画リストを取得 (site で iwara.tv / iwara.ai 切替)。
        /// Status は Success/UserNotFound(確定) と Failed(ログイン未済・通信エラー等、今回は
        /// 判定できなかった) を区別する。呼び出し側はアカウント消滅フラグ等の永続状態を
        /// Failed のときには変更してはいけない(一時的な403/レート制限を消滅と誤判定するため)。
        /// </summary>
        public async Task<(List<VideoInfo> Videos, ChannelFetchStatus Status)> GetUserVideosAsync(string username, IProgress<string>? progress = null, string? site = null, CancellationToken ct = default)
        {
            if (!IsLoggedIn)
            {
                progress?.Report("LOGIN_REQUIRED: " + Utils.L.T("Svc_LoginRequired"));
                return (new List<VideoInfo>(), ChannelFetchStatus.Failed);
            }

            progress?.Report(L.T("SvcIwaraApiService_D001", username));

            // Rust helperの正式なアクション名は kebab-case。旧Python互換の
            // snake_caseをここで渡すと、ヘルパー側で Unknown action になる。
            var result = await RunRustAsync("get-videos", site, ct, username);

            if (result == null)
            {
                progress?.Report(L.T("SvcIwaraApiService_D002"));
                return (new List<VideoInfo>(), ChannelFetchStatus.Failed);
            }

            var root = result.RootElement;

            if (!root.TryGetProperty("success", out var success) || !success.GetBoolean())
            {
                var error = root.TryGetProperty("error", out var errorProp)
                    ? errorProp.GetString()
                    : "Unknown error";
                progress?.Report(L.T("SvcIwaraApiService_D003", error));
                var code = root.TryGetProperty("code", out var codeProp) ? codeProp.GetString() : null;
                var status = code == "USER_NOT_FOUND" ? ChannelFetchStatus.UserNotFound : ChannelFetchStatus.Failed;
                return (new List<VideoInfo>(), status);
            }

            var videos = new List<VideoInfo>();
            
            if (root.TryGetProperty("videos", out var videosArray))
            {
                foreach (var video in videosArray.EnumerateArray())
                {
                    DateTime? postedAt = null;
                    if (video.TryGetProperty("created_at", out var ca) && ca.ValueKind == JsonValueKind.String
                        && DateTime.TryParse(ca.GetString(), out var caDt))
                    {
                        postedAt = caDt;
                    }

                    var apiRawJson = video.TryGetProperty("raw", out var rawEl) ? rawEl.GetRawText() : "";

                    var videoInfo = new VideoInfo
                    {
                        VideoId = video.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                        Title = video.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "",
                        ThumbnailUrl = video.TryGetProperty("thumbnail", out var thumb) ? thumb.GetString() ?? "" : "",
                        DurationSeconds = video.TryGetProperty("duration", out var dur) && dur.ValueKind == JsonValueKind.Number
                            ? (int)dur.GetDouble() : 0,
                        EmbedUrl = video.TryGetProperty("embed_url", out var embed) ? embed.GetString() ?? "" : "",
                        Rating = video.TryGetProperty("rating", out var rt) ? rt.GetString() ?? "" : "",
                        PostedAt = postedAt,
                        ApiRawJson = apiRawJson,
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
            progress?.Report(L.T("SvcIwaraApiService_D004", count));

            return (videos, ChannelFetchStatus.Success);
        }

        /// <summary>
        /// ダウンロードURLを取得 (file_id / author 情報込み, site で iwara.tv / iwara.ai 切替)。
        /// site 未指定で iwara.tv で叩いて "errors.differentSite" が返った場合は自動で iwara.ai を再試行する
        /// (ローカルファイルから逆引きする ImportFromFolderWizard 等で site が不明なケース向け)。
        /// </summary>
        public async Task<VideoUrlInfo> GetDownloadUrlAsync(string videoId, string? site = null)
        {
            if (!IsLoggedIn)
                return VideoUrlInfo.FromError("LOGIN_REQUIRED: " + Utils.L.T("Svc_LoginRequired"));

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
            // Rust helperの正式なアクション名は get-url。
            var result = await RunRustAsync("get-url", site, videoId);

            if (result == null)
                return VideoUrlInfo.FromError(L.T("SvcIwaraApiService_D002"));

            var root = result.RootElement;

            if (root.TryGetProperty("success", out var success) && success.GetBoolean())
            {
                DateTime? postedAt = null;
                if (root.TryGetProperty("created_at", out var ca) && ca.ValueKind == JsonValueKind.String
                    && DateTime.TryParse(ca.GetString(), out var caDt))
                {
                    postedAt = caDt;
                }

                var apiRawJson = root.TryGetProperty("raw", out var rawEl) ? rawEl.GetRawText() : null;

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
                    ThumbnailUrl = GetString(root, "thumbnail"),
                    PostedAt = postedAt,
                    ApiRawJson = apiRawJson,
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
            public string? ThumbnailUrl { get; set; }
            public DateTime? PostedAt { get; set; }
            public string? ApiRawJson { get; set; }
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
        /// 動画をダウンロード(Rust helper、site で iwara.tv / iwara.ai 切替)
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
                return (false, "LOGIN_REQUIRED: " + Utils.L.T("Svc_LoginRequired"));

            progress?.Report(L.T("SvcIwaraApiService_D005", videoId));

            var result = await RunRustWithProgressAsync("download", percentProgress, ct, site, videoId, outputPath);
            
            if (result == null)
                return (false, "Rust helperの実行に失敗しました");

            var root = result.RootElement;
            
            if (root.TryGetProperty("success", out var success) && success.GetBoolean())
            {
                progress?.Report(L.T("SvcIwaraApiService_D006"));
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

            progress?.Report(L.T("SvcIwaraApiService_D007", embedUrl));

            var ytDlpPath = Utils.SettingsManager.Instance.Settings.YtDlpPath;
            if (string.IsNullOrWhiteSpace(ytDlpPath))
                ytDlpPath = "yt-dlp";

            var result = await RunRustWithProgressAsync(
                "download_external",
                percentProgress,
                ct,
                embedUrl,
                outputPath,
                "--yt-dlp-path",
                ytDlpPath);

            if (result == null)
                return (false, "Rust helperの実行に失敗しました", null);

            var root = result.RootElement;

            if (root.TryGetProperty("success", out var success) && success.GetBoolean())
            {
                var filePath = root.TryGetProperty("file_path", out var fpProp) ? fpProp.GetString() : null;
                progress?.Report(L.T("SvcIwaraApiService_D008"));
                return (true, null, filePath);
            }

            var error = root.TryGetProperty("error", out var errorProp)
                ? errorProp.GetString()
                : "Unknown error";

            return (false, error, null);
        }

        /// <summary>
        /// Rust helperを実行(進捗リアルタイム取得、site 指定可)
        /// </summary>
        private Task<JsonDocument?> RunRustWithProgressAsync(string action, IProgress<double>? percentProgress, CancellationToken ct, params string[] args)
            => RunRustWithProgressAsync(action, percentProgress, ct, null, args);

        private async Task<JsonDocument?> RunRustWithProgressAsync(string action, IProgress<double>? percentProgress, CancellationToken ct, string? site, params string[] args)
        {
            if (!IsRustConfigured)
            {
                Debug.WriteLine("Rust helper not configured");
                return null;
            }

            var allArgs = new List<string> { action };
            allArgs.AddRange(args);

            if (!string.IsNullOrEmpty(site))
            {
                allArgs.Add("--site");
                allArgs.Add(site);
            }

            allArgs.AddRange(GetRateLimitArgs());
            if (!Utils.SettingsManager.Instance.Settings.EnableExponentialBackoff)
                allArgs.Add("--no-backoff");

            var psi = new ProcessStartInfo
            {
                FileName = RustHelperPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
                WorkingDirectory = _appDir
            };
            foreach (var argument in allArgs)
                psi.ArgumentList.Add(argument);
            if (!string.IsNullOrEmpty(_token))
            {
                psi.EnvironmentVariables["IWARA_TOKEN"] = _token;
            }

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
                    Debug.WriteLine($"Rust stderr: {e.Data}");
                    
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
                        LoggingService.Instance.Warn($"Rust: {e.Data}");
                    }
                }
            };

            process.Start();
            Utils.ChildProcessJob.AssignProcess(process); // 親死亡で自動 Kill
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
                        Debug.WriteLine("Rust helper did not exit within 5s after Kill");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Rust helper Kill failed: {ex.Message}");
                }
                throw;
            }

            var outputStr = output.ToString().Trim();

            if (string.IsNullOrEmpty(outputStr))
            {
                var errorStr = errorOutput.ToString().Trim();
                if (!string.IsNullOrEmpty(errorStr))
                {
                    LoggingService.Instance.Error($"Rust helper実行エラー (action={action}):\n{errorStr}");
                }
                return null;
            }

            try
            {
                return JsonDocument.Parse(outputStr);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Rust JSON parse error: {ex.Message}");
                LoggingService.Instance.Error($"Rust出力JSONパースエラー: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 同梱Rust helperの配置を確認し、セットアップ完了を記録する。
        /// </summary>
        public async Task<bool> RunSetupAsync(string? helperPath, IProgress<string>? progress = null)
        {
            await Task.Yield();
            var candidate = string.IsNullOrWhiteSpace(helperPath)
                ? _rustHelperPath
                : (Path.IsPathRooted(helperPath) ? helperPath : Path.Combine(_appDir, helperPath));
            progress?.Report("Rust helperを確認しています...");
            if (!File.Exists(candidate))
            {
                var message = $"Rust helperが見つかりません: {candidate}";
                LoggingService.Instance.Error($"[セットアップ] {message}");
                progress?.Report(message);
                return false;
            }

            Utils.SettingsManager.Instance.Settings.RustHelperPath = candidate;
            Utils.SettingsManager.Instance.Save();
            try
            {
                await File.WriteAllTextAsync(
                    SetupMarkerPath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\nhelper={candidate}\n");
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Warn($"[セットアップ] マーカー作成失敗: {ex.Message}");
            }
            progress?.Report("Rust helperのセットアップが完了しました。");
            return true;
        }

        /// <summary>
        /// 環境チェック
        /// </summary>
        public (bool RustReady, bool HelperReady) CheckEnvironment()
        {
            return (IsRustConfigured, IsScriptReady);
        }
    }
}
