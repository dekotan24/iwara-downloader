using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace IwaraDownloader.Services
{
    /// <summary>
    /// アプリケーションの更新チェックサービス
    /// </summary>
    public class UpdateService
    {
        // 既定の100秒タイムアウトだと設定画面の手動チェックでUIが固まるので短縮
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
        private const string GitHubApiUrl = "https://api.github.com/repos/dekotan24/iwara-downloader/releases/latest";
        private const string ReleasesPageUrl = "https://github.com/dekotan24/iwara-downloader/releases";

        /// <summary>
        /// 現在のアプリケーションバージョンを取得
        /// </summary>
        public static Version CurrentVersion
        {
            get
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return version ?? new Version(1, 0, 0);
            }
        }

        /// <summary>
        /// バージョン文字列を取得
        /// </summary>
        public static string CurrentVersionString => $"v{CurrentVersion.Major}.{CurrentVersion.Minor}.{CurrentVersion.Build}";

        /// <summary>
        /// 最新バージョンをチェック
        /// </summary>
        public static async Task<UpdateCheckResult> CheckForUpdateAsync()
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "IwaraDownloader");

                var response = await _httpClient.GetAsync(GitHubApiUrl);
                
                if (!response.IsSuccessStatusCode)
                {
                    return new UpdateCheckResult
                    {
                        Success = false,
                        ErrorMessage = $"GitHub API error: {response.StatusCode}"
                    };
                }

                var release = await response.Content.ReadFromJsonAsync<GitHubRelease>();
                
                if (release == null || string.IsNullOrEmpty(release.TagName))
                {
                    return new UpdateCheckResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid response from GitHub"
                    };
                }

                // タグ名からバージョンを解析 (v1.0.0 形式)
                var tagVersion = release.TagName.TrimStart('v', 'V');
                if (!Version.TryParse(tagVersion, out var latestVersion))
                {
                    return new UpdateCheckResult
                    {
                        Success = false,
                        ErrorMessage = $"Invalid version format: {release.TagName}"
                    };
                }

                // GitHub のタグは "v2.0" (2要素) と "v1.1.1" (3要素) が混在する。
                // Version.Parse("2.0") は Build=-1 になり、"2.0.0.0" との比較が不安定なので
                // 両辺を Major.Minor.Build に正規化 (未定義 -1 は 0 扱い) してから比較する。
                var hasUpdate = NormalizeVersion(latestVersion) > NormalizeVersion(CurrentVersion);

                return new UpdateCheckResult
                {
                    Success = true,
                    HasUpdate = hasUpdate,
                    LatestVersion = latestVersion,
                    LatestVersionString = release.TagName,
                    ReleaseUrl = release.HtmlUrl ?? ReleasesPageUrl,
                    ReleaseNotes = release.Body ?? "",
                    PublishedAt = release.PublishedAt
                };
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error("Update check failed", ex);
                return new UpdateCheckResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// バージョンを Major.Minor.Build に正規化する。
        /// Version.Parse は未指定要素を -1 にするため、それを 0 に丸めて比較を安定させる。
        /// 例: "2.0" → 2.0.0, "2.0.0.0" → 2.0.0
        /// </summary>
        private static Version NormalizeVersion(Version v)
            => new Version(Math.Max(0, v.Major), Math.Max(0, v.Minor), Math.Max(0, v.Build));

        /// <summary>
        /// リリースページを開く
        /// </summary>
        public static void OpenReleasesPage()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = ReleasesPageUrl,
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }

    /// <summary>
    /// 更新チェック結果
    /// </summary>
    public class UpdateCheckResult
    {
        public bool Success { get; set; }
        public bool HasUpdate { get; set; }
        public Version? LatestVersion { get; set; }
        public string LatestVersionString { get; set; } = "";
        public string ReleaseUrl { get; set; } = "";
        public string ReleaseNotes { get; set; } = "";
        public DateTime? PublishedAt { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// GitHub Release API レスポンス
    /// </summary>
    internal class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("published_at")]
        public DateTime? PublishedAt { get; set; }
    }
}
