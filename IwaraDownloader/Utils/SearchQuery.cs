using IwaraDownloader.Models;

namespace IwaraDownloader.Utils
{
    /// <summary>
    /// 動画リストの検索クエリ。フリーテキスト + フィールド指定 + 除外指定をサポート。
    ///
    /// サポート構文:
    ///   foo           → title/author/tags/memo のいずれかに "foo" を含む (大小無視)
    ///   foo bar       → foo AND bar (各語が何れかのフィールドにマッチ)
    ///   -bot          → "bot" を含まない (除外)
    ///   tag:vr        → tags に vr を含む
    ///   author:fooo   → AuthorUsername に foo を含む
    ///   memo:推し     → memo に "推し" を含む
    ///   status:failed → ステータスが failed (failed/done/wip/err 等のエイリアスもOK)
    ///   id:xxx        → VideoId に xxx を含む
    ///   "二語以上"    → 引用符内をひと塊として扱う (基本構文と組み合わせ可)
    /// </summary>
    public class SearchQuery
    {
        private readonly List<Term> _terms = new();
        public bool IsEmpty => _terms.Count == 0;

        /// <summary>
        /// フリーテキスト (フィールド未指定) の語を AuthorUsername にもマッチさせるか。
        /// アーティスト選択中は作者名が全動画で共通なため、作者名に部分一致する語で
        /// 全件ヒットしてしまう。その場合は false にしてタイトル等のみで絞り込む。
        /// 明示的な author: 指定はこのフラグの影響を受けない。
        /// </summary>
        public bool IncludeAuthorInFreeText { get; set; } = true;

        public static SearchQuery Parse(string input)
        {
            var q = new SearchQuery();
            if (string.IsNullOrWhiteSpace(input)) return q;

            // トークナイザ: 引用符で囲んだ範囲はひと塊扱い
            foreach (var token in Tokenize(input))
            {
                if (string.IsNullOrEmpty(token)) continue;
                bool negate = false;
                var t = token;
                if (t.StartsWith("-") && t.Length > 1)
                {
                    negate = true;
                    t = t[1..];
                }

                string field = ""; // "" = 全フィールド
                string value = t;
                var colonIdx = t.IndexOf(':');
                if (colonIdx > 0)
                {
                    field = t[..colonIdx].ToLowerInvariant();
                    value = t[(colonIdx + 1)..];
                }
                // 引用符が値の前後に付いてたら剥がす
                if (value.Length >= 2 && value.StartsWith('"') && value.EndsWith('"'))
                    value = value[1..^1];
                if (string.IsNullOrEmpty(value)) continue;

                q._terms.Add(new Term(field, value.ToLowerInvariant(), negate));
            }
            return q;
        }

        private static IEnumerable<string> Tokenize(string input)
        {
            var sb = new System.Text.StringBuilder();
            bool inQuote = false;
            foreach (var ch in input)
            {
                if (ch == '"')
                {
                    inQuote = !inQuote;
                    sb.Append(ch);
                }
                else if (char.IsWhiteSpace(ch) && !inQuote)
                {
                    if (sb.Length > 0) { yield return sb.ToString(); sb.Clear(); }
                }
                else
                {
                    sb.Append(ch);
                }
            }
            if (sb.Length > 0) yield return sb.ToString();
        }

        public bool Match(VideoInfo v)
        {
            // 全 term が成立する (AND)
            foreach (var t in _terms)
            {
                // 未知フィールド (typo 等) は term ごと無視 → クエリ全体が穴にならない
                if (t.Field != "" && !FieldMap.ContainsKey(t.Field)) continue;

                bool hit = MatchTerm(v, t);
                if (t.Negate) hit = !hit;
                if (!hit) return false;
            }
            return true;
        }

        // フィールド名 → VideoInfo の値の引き出し関数 (未知フィールド判定用 dict)
        private static readonly System.Collections.Generic.Dictionary<string, System.Func<VideoInfo, string>> FieldMap
            = new()
            {
                ["title"] = v => v.Title,
                ["author"] = v => v.AuthorUsername,
                ["tag"] = v => v.Tags,
                ["tags"] = v => v.Tags,
                ["memo"] = v => v.Memo,
                ["id"] = v => v.VideoId,
                ["uuid"] = v => v.FileUuid,
                ["url"] = v => v.Url,
                ["status"] = v => StatusToText(v.Status),
                ["rating"] = v => v.Rating,
                ["site"] = v => v.Site,
                ["fav"] = v => v.IsFavorite ? "true" : "false",
                ["favorite"] = v => v.IsFavorite ? "true" : "false",
            };

        private bool MatchTerm(VideoInfo v, Term t)
        {
            if (t.Field == "")
            {
                // 全フィールド検索: title / author / tags / memo / status
                return ContainsCI(v.Title, t.Value)
                    || (IncludeAuthorInFreeText && ContainsCI(v.AuthorUsername, t.Value))
                    || ContainsCI(v.Tags, t.Value)
                    || ContainsCI(v.Memo, t.Value)
                    || ContainsCI(StatusToText(v.Status), t.Value);
            }

            // 未知フィールドは無効扱い (Negate 経由でも常に false に固定して
            // "-unknown:foo" が全件ヒットしないようにする)
            if (!FieldMap.TryGetValue(t.Field, out var getter)) return false;

            // status はエイリアス考慮で部分一致
            if (t.Field == "status")
            {
                return ContainsCI(getter(v), NormalizeStatusKeyword(t.Value));
            }

            // site フィールドは旧データ (空文字) を iwara.tv 扱いする
            if (t.Field == "site")
            {
                var siteValue = string.IsNullOrEmpty(v.Site) ? "www.iwara.tv" : v.Site;
                return ContainsCI(siteValue, t.Value);
            }

            return ContainsCI(getter(v) ?? "", t.Value);
        }

        private static bool ContainsCI(string s, string sub)
            => !string.IsNullOrEmpty(s) && s.ToLowerInvariant().Contains(sub);

        private static string StatusToText(DownloadStatus s) => s switch
        {
            DownloadStatus.Pending => "pending",
            DownloadStatus.Downloading => "downloading",
            DownloadStatus.Completed => "completed",
            DownloadStatus.Failed => "failed",
            DownloadStatus.Skipped => "skipped",
            DownloadStatus.Paused => "paused",
            DownloadStatus.WritingTags => "writingtags",
            _ => "unknown"
        };

        private static string NormalizeStatusKeyword(string key) => key switch
        {
            "done" or "ok" => "completed",
            "wip" or "dl" => "downloading",
            "wait" or "queue" => "pending",
            "err" or "error" or "fail" => "failed",
            "skip" => "skipped",
            "pause" => "paused",
            _ => key
        };

        private readonly record struct Term(string Field, string Value, bool Negate);
    }
}
