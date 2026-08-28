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
    /// iwara 検索クライアント。IwaraApiService のRust helperに「search」actionで問い合わせる。
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
                using var doc = await _api.SearchVideosAsync(query, page, limit, site);
                if (doc == null)
                {
                    result.Error = "Rust helperの応答がありません";
                    return result;
                }
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
