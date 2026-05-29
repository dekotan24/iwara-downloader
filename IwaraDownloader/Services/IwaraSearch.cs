using System.Diagnostics;
using System.Text;
using System.Text.Json;
using IwaraDownloader.Models;

namespace IwaraDownloader.Services
{
    /// <summary>
    /// 検索結果アイテム (UI 表示用)
    /// </summary>
    public class SearchResultItem
    {
        public string VideoId { get; set; } = "";
        public string Title { get; set; } = "";
        public string AuthorUsername { get; set; } = "";
        public string AuthorName { get; set; } = "";
        public string ThumbnailUrl { get; set; } = "";
        public int DurationSeconds { get; set; }
        public string Rating { get; set; } = "";
        public string EmbedUrl { get; set; } = "";
        public DateTime? CreatedAt { get; set; }
        public bool IsPrivate { get; set; }

        /// <summary>検索した所属サイト (www.iwara.tv / www.iwara.ai)。URL 生成に使う。</summary>
        public string Site { get; set; } = "www.iwara.tv";

        /// <summary>既に DB に登録済みか (UI 表示・選択制御用)。</summary>
        public bool AlreadyInDb { get; set; }

        public string Url => $"https://{(string.IsNullOrEmpty(Site) ? "www.iwara.tv" : Site)}/video/{VideoId}";
        public string DurationFormatted
        {
            get
            {
                var ts = TimeSpan.FromSeconds(DurationSeconds);
                return ts.Hours > 0
                    ? $"{ts.Hours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                    : $"{ts.Minutes}:{ts.Seconds:D2}";
            }
        }
    }

    /// <summary>
    /// iwara 検索結果ページ
    /// </summary>
    public class SearchResultPage
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int Limit { get; set; }
        public List<SearchResultItem> Items { get; set; } = new();
    }

    /// <summary>
    /// iwara 検索クライアント。IwaraApiService の Python ヘルパーに「search」action で問い合わせる。
    /// </summary>
    public class IwaraSearch
    {
        private readonly IwaraApiService _api;
        public IwaraSearch(IwaraApiService api) { _api = api; }

        public async Task<SearchResultPage> SearchAsync(string query, int page = 0, int limit = 32, string? site = null)
        {
            var result = new SearchResultPage { Page = page, Limit = limit };
            if (!_api.IsLoggedIn)
            {
                result.Error = "ログインが必要です";
                return result;
            }
            try
            {
                // IwaraApiService に直接アクセスする手段が無いので Process を呼ぶ
                // トークンは環境変数経由 (コマンドライン引数からの漏洩防止)
                var siteArg = string.IsNullOrEmpty(site) ? "" : $" --site \"{site}\"";
                var psi = new ProcessStartInfo
                {
                    FileName = Utils.SettingsManager.Instance.Settings.PythonPath,
                    Arguments = $"\"{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "iwara_helper.py")}\" search \"{query.Replace("\"", "\\\"")}\" {page} {limit}{siteArg}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                };
                if (!string.IsNullOrEmpty(_api.Token))
                {
                    psi.EnvironmentVariables["IWARA_TOKEN"] = _api.Token;
                }
                using var proc = new Process { StartInfo = psi };
                var stdoutBuf = new StringBuilder();
                var stderrBuf = new StringBuilder();
                proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdoutBuf.AppendLine(e.Data); };
                proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderrBuf.AppendLine(e.Data); };
                proc.Start();
                Utils.ChildProcessJob.AssignProcess(proc); // 親死亡で自動 Kill
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                await proc.WaitForExitAsync();
                proc.WaitForExit(); // ストリーム flush 完了を保証

                var stdout = stdoutBuf.ToString();
                if (string.IsNullOrWhiteSpace(stdout))
                {
                    result.Error = string.IsNullOrWhiteSpace(stderrBuf.ToString())
                        ? "Python 応答なし"
                        : $"Python エラー: {stderrBuf.ToString().Trim()}";
                    return result;
                }
                using var doc = JsonDocument.Parse(stdout);
                var root = doc.RootElement;
                if (!root.TryGetProperty("success", out var s) || !s.GetBoolean())
                {
                    result.Error = root.TryGetProperty("error", out var e) ? e.GetString() : "Unknown";
                    return result;
                }
                result.Success = true;
                result.TotalCount = root.TryGetProperty("count", out var c) ? c.GetInt32() : 0;

                if (root.TryGetProperty("videos", out var vidArr))
                {
                    foreach (var v in vidArr.EnumerateArray())
                    {
                        var item = new SearchResultItem
                        {
                            VideoId = GetStr(v, "id"),
                            Title = GetStr(v, "title"),
                            AuthorUsername = GetStr(v, "author_username"),
                            AuthorName = GetStr(v, "author_name"),
                            ThumbnailUrl = GetStr(v, "thumbnail"),
                            DurationSeconds = v.TryGetProperty("duration", out var dur) && dur.ValueKind == JsonValueKind.Number
                                ? (int)dur.GetDouble() : 0,
                            Rating = GetStr(v, "rating"),
                            EmbedUrl = GetStr(v, "embed_url"),
                            IsPrivate = v.TryGetProperty("private", out var pv) && pv.ValueKind == JsonValueKind.True,
                            Site = string.IsNullOrEmpty(site) ? "www.iwara.tv" : site,
                        };
                        if (v.TryGetProperty("created_at", out var ca) && ca.ValueKind == JsonValueKind.String
                            && DateTime.TryParse(ca.GetString(), out var dt))
                        {
                            item.CreatedAt = dt;
                        }
                        result.Items.Add(item);
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                result.Error = $"検索失敗: {ex.Message}";
                return result;
            }
        }

        private static string GetStr(JsonElement root, string name)
            => root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";
    }
}
