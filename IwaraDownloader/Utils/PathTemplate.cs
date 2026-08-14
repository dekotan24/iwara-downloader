using System.Globalization;

namespace IwaraDownloader.Utils
{
    /// <summary>
    /// ファイル名/フォルダ構成のテンプレート文字列 (例: "{artist}\{id}_{title}.mp4") から、
    /// 実ファイルのパスに対して {id} / {title} / {artist} / {date} の各プレースホルダの値を抜き出す。
    /// {date} は yyyy-MM-dd / yyyy_MM_dd / yyyy.MM.dd / yyyyMMdd を自動認識する。
    ///
    /// このクラスは抽出器 (extractor) であって照合エンジンではない。抽出した値は
    /// 呼び出し側が FilenameMatcher に渡して照合に使う。優先順位 (ID→完全一致→部分一致→接頭辞一致)
    /// はあくまで FilenameMatcher 側の1本のロジックに任せ、ここでは分岐させない。
    /// </summary>
    public static class PathTemplate
    {
        /// <summary>抽出結果。値が取れなかったプレースホルダは null</summary>
        public class ExtractResult
        {
            public string? IdValue { get; init; }
            public string? TitleValue { get; init; }
            public string? ArtistValue { get; init; }
            public string? DateValue { get; init; }
        }

        /// <summary>
        /// テンプレートをパスセパレータで分割した際のセグメント数と、実ファイルの
        /// scanRoot からの相対セグメント数が一致しないなど、そもそも解釈できない場合は null。
        /// </summary>
        /// <param name="template">例: "{title}.mp4" / "{id}_{title}.mp4" / "{date}_{title}.mp4" / "{artist}\{title}.mp4"</param>
        /// <param name="filePath">実ファイルの絶対パス</param>
        /// <param name="scanRoot">スキャン起点フォルダ (テンプレートのパス部分はここからの相対と解釈)</param>
        /// <param name="knownArtistUsernames">
        /// {artist} を含むファイル名セグメントの区切り解決に使う、既知のアーティストユーザー名
        /// (サニタイズ前の生の値)。単一アーティストモードでは対象アーティスト1件だけを渡せばよい。
        /// </param>
        /// <param name="knownIds">
        /// {id} を含むファイル名セグメントの区切り解決に使う、既知の videoId 集合。
        /// </param>
        public static ExtractResult? Extract(
            string template, string filePath, string scanRoot,
            IEnumerable<string> knownArtistUsernames, IReadOnlySet<string> knownIds)
        {
            if (string.IsNullOrWhiteSpace(template)) return null;

            var templateSegments = template.Split('\\', '/')
                .Where(s => s.Length > 0)
                .ToList();
            if (templateSegments.Count == 0) return null;

            // 実ファイルの scanRoot からの相対セグメント (フォルダ名... + ファイル名) を得る
            string relative;
            try
            {
                var rootFull = Path.GetFullPath(scanRoot).TrimEnd('\\', '/');
                var fileFull = Path.GetFullPath(filePath);
                if (!fileFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)) return null;
                relative = fileFull.Substring(rootFull.Length).TrimStart('\\', '/');
            }
            catch { return null; }

            var fileSegments = relative.Split('\\', '/').Where(s => s.Length > 0).ToList();
            if (fileSegments.Count != templateSegments.Count) return null; // 深さが合わない = 解釈しない

            string? id = null, title = null, artist = null, date = null;
            var artistList = knownArtistUsernames
                .Where(a => !string.IsNullOrEmpty(a))
                .Select(a => Helpers.SanitizeFileName(a))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                // 長い名前から試す (短い名前が長い名前の接頭辞になっているケースの誤判定防止)
                .OrderByDescending(a => a.Length)
                .ToList();

            for (int i = 0; i < templateSegments.Count; i++)
            {
                var tSeg = templateSegments[i];
                var fSeg = fileSegments[i];
                bool isLastSegment = i == templateSegments.Count - 1;

                if (isLastSegment)
                {
                    // ファイル名セグメント: 拡張子を落としてから解析
                    var tName = StripKnownExtension(tSeg);
                    var fName = Path.GetFileNameWithoutExtension(fSeg);
                    if (!ParseFileNameSegment(tName, fName, knownIds, artistList,
                            ref id, ref title, ref artist, ref date))
                        return null;
                }
                else
                {
                    // フォルダセグメント: 単一プレースホルダのみ対応 ("{artist}" 等)
                    if (!TryGetSolePlaceholder(tSeg, out var placeholder)) return null;
                    if (!AssignPlaceholder(placeholder, fSeg, ref id, ref title, ref artist, ref date))
                        return null;
                }
            }

            if (id == null && title == null && artist == null && date == null) return null;
            return new ExtractResult
            {
                IdValue = id,
                TitleValue = title,
                ArtistValue = artist,
                DateValue = date,
            };
        }

        private static string StripKnownExtension(string templateFileName)
        {
            var idx = templateFileName.LastIndexOf('.');
            return idx > 0 ? templateFileName.Substring(0, idx) : templateFileName;
        }

        private static bool TryGetSolePlaceholder(string segment, out string placeholder)
        {
            placeholder = "";
            if (segment.Length < 3 || segment[0] != '{' || segment[^1] != '}') return false;
            placeholder = Normalize(segment.Substring(1, segment.Length - 2));
            return placeholder is "id" or "title" or "artist" or "date";
        }

        /// <summary>{author} は {artist} のエイリアス (設定画面のファイル名テンプレートの語彙に合わせる)</summary>
        private static string Normalize(string placeholder) =>
            placeholder.Equals("author", StringComparison.OrdinalIgnoreCase) ? "artist" : placeholder.ToLowerInvariant();

        private static bool AssignPlaceholder(
            string placeholder, string value,
            ref string? id, ref string? title, ref string? artist, ref string? date)
        {
            switch (placeholder)
            {
                case "id": id = value; return true;
                case "title": title = value; return true;
                case "artist": artist = value; return true;
                case "date":
                    if (!IsSupportedDate(value)) return false;
                    date = value;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// ファイル名セグメントのテンプレート
        /// (例: "{id}_{title}", "{date}_{title}", "{title}", "{artist}_{title}") を
        /// 実ファイル名と突き合わせて各プレースホルダの値を抜き出す。
        /// 単一プレースホルダのみなら丸ごとそれが値。複数プレースホルダは既知集合を錨にして分割する
        /// (SanitizeFileName が連続する区切り文字を1つに潰すため、区切りの個数を前提にした厳密な
        /// 正規表現では実ファイルと食い違う。既知の id / artist 名を手がかりに緩く探す)。
        /// </summary>
        private static bool ParseFileNameSegment(
            string tName, string fName, IReadOnlySet<string> knownIds, List<string> knownArtists,
            ref string? id, ref string? title, ref string? artist, ref string? date)
        {
            if (TryGetSolePlaceholder(tName, out var sole))
            {
                return AssignPlaceholder(sole, fName, ref id, ref title, ref artist, ref date);
            }

            var placeholders = ExtractPlaceholderOrder(tName);
            if (placeholders.Count == 0) return false; // プレースホルダ無しのテンプレートは解釈しない

            // {id}_{title} 系: 先頭のトークンが既知IDと完全一致するかを錨にする
            if (placeholders.Count == 2 && placeholders[0] == "id" && placeholders[1] == "title")
            {
                var sepIdx = FindFirstSeparatorAfterToken(fName);
                if (sepIdx <= 0) return false;
                var idCandidate = fName.Substring(0, sepIdx);
                if (!knownIds.Contains(idCandidate)) return false;
                id = idCandidate;
                title = fName.Substring(sepIdx).TrimStart('_', '-', ' ');
                return true;
            }

            // {date}_{title} 系: 日付は複数の一般的な形式を自動認識する。
            // テンプレート中の区切り文字にかかわらず、日付直後の _, -, 空白, . を区切りとして扱う。
            if (placeholders.Count == 2 && placeholders[0] == "date" && placeholders[1] == "title")
            {
                if (!TrySplitLeadingDate(fName, out var dateValue, out var titleValue)) return false;
                date = dateValue;
                title = titleValue;
                return true;
            }

            // {artist}_{title} 系: 既知アーティスト名 (長い順) を接頭辞として試す
            if (placeholders.Count == 2 && placeholders[0] == "artist" && placeholders[1] == "title")
            {
                foreach (var a in knownArtists)
                {
                    if (fName.StartsWith(a, StringComparison.OrdinalIgnoreCase))
                    {
                        var rest = fName.Substring(a.Length).TrimStart('_', '-', ' ');
                        if (rest.Length == 0) continue;
                        artist = a;
                        title = rest;
                        return true;
                    }
                }
                return false;
            }

            // それ以外の組み合わせ (3プレースホルダ混在等) は今のところ非対応
            return false;
        }

        private static readonly string[] SupportedDateFormats =
        {
            "yyyy-MM-dd",
            "yyyy_MM_dd",
            "yyyy.MM.dd",
            "yyyyMMdd",
        };

        private static bool IsSupportedDate(string value) =>
            DateTime.TryParseExact(
                value,
                SupportedDateFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _);

        /// <summary>
        /// ファイル名先頭の日付らしきプレフィックスを検出して切り離す (内部再利用向け、public)。
        /// {date} プレースホルダの解釈だけでなく、テンプレート未指定/{title} のみ指定時に
        /// FilenameMatcher が「タイトルに日付が紛れ込んだ」ケースを救済するためにも使う
        /// (Issue #20: 既定テンプレートのままだと日付プレフィックス付きファイル名の短い
        /// CJK タイトルが MinFuzzyMatchLength の壁で一致しない問題への対策)。
        /// </summary>
        internal static bool TrySplitLeadingDate(string fileName, out string date, out string title)
        {
            date = "";
            title = "";

            // 区切りあり3形式は10文字、区切り無し yyyyMMdd は8文字。
            foreach (var length in new[] { 10, 8 })
            {
                if (fileName.Length <= length) continue;

                var candidate = fileName.Substring(0, length);
                if (!IsSupportedDate(candidate)) continue;

                var remainder = fileName.Substring(length);
                if (remainder.Length == 0 || !IsDateTitleSeparator(remainder[0])) continue;

                remainder = remainder.TrimStart('_', '-', ' ', '.');
                if (remainder.Length == 0) continue;

                date = candidate;
                title = remainder;
                return true;
            }

            return false;
        }

        private static bool IsDateTitleSeparator(char c) => c is '_' or '-' or ' ' or '.';

        /// <summary>先頭の英数字トークンの直後 (区切り文字の開始位置) を返す。無ければ -1</summary>
        private static int FindFirstSeparatorAfterToken(string fileName)
        {
            int i = 0;
            while (i < fileName.Length && char.IsLetterOrDigit(fileName[i])) i++;
            return i < fileName.Length ? i : -1;
        }

        private static List<string> ExtractPlaceholderOrder(string template)
        {
            var result = new List<string>();
            int i = 0;
            while (i < template.Length)
            {
                if (template[i] == '{')
                {
                    var end = template.IndexOf('}', i);
                    if (end < 0) break;
                    result.Add(Normalize(template.Substring(i + 1, end - i - 1)));
                    i = end + 1;
                }
                else i++;
            }
            return result;
        }
    }
}
