using System.Text.RegularExpressions;
using IwaraDownloader.Models;

namespace IwaraDownloader.Utils
{
    /// <summary>
    /// タイトル文字列とファイル名の一致度。数値が小さいほど確度が高い
    /// (Tier の比較に enum の宣言順を使うため、順序を変えないこと)。
    /// </summary>
    public enum TitleMatchTier
    {
        /// <summary>パス中のトークン (ファイル名 or フォルダ名) が既知の videoId と完全一致</summary>
        Id,
        /// <summary>サニタイズ済みタイトル == ファイル名(拡張子無し)</summary>
        Exact,
        /// <summary>どちらかがもう一方を部分文字列として含む</summary>
        Substring,
        /// <summary>短い方が長い方の先頭に一致 (Title の200字切り詰め等を救済)</summary>
        Prefix,
    }

    /// <summary>曖昧と判定された理由 (UI 層で翻訳キーに変換するため、文字列でなく enum で持つ)</summary>
    public enum AmbiguousReason
    {
        None,
        /// <summary>同一ファイルが (最良Tier・最長タイトルでも) 複数の動画候補と一致した</summary>
        MultipleCandidatesForFile,
        /// <summary>同一の動画候補が複数のファイルから選ばれた</summary>
        MultipleFilesForCandidate,
    }

    /// <summary>タイトル照合で見つかった1件の候補</summary>
    public class TitleMatchCandidate
    {
        public string FilePath { get; init; } = "";
        public VideoInfo Video { get; init; } = null!;
        public TitleMatchTier Tier { get; init; }

        /// <summary>
        /// true の場合、同一ファイルが複数候補と / 同一候補が複数ファイルと一致していて
        /// 自動採用できない (要手動確認)
        /// </summary>
        public bool Ambiguous { get; init; }
        public AmbiguousReason AmbiguousReason { get; init; } = AmbiguousReason.None;

        /// <summary>
        /// AmbiguousReason.MultipleCandidatesForFile の場合のみ、同点だった候補全員
        /// (Video を含む) を保持する。UI 側でドロップダウン選択肢として使う。
        /// それ以外の場合は null。
        /// </summary>
        public List<VideoInfo>? AlternativeCandidates { get; init; }
    }

    /// <summary>Match() の結果一式</summary>
    public class FilenameMatchResult
    {
        public List<TitleMatchCandidate> Matches { get; init; } = new();

        /// <summary>
        /// パス中に既知の videoId トークンが見つかったが、その動画は既に別の場所にDL済みだった
        /// ファイル (＝重複コピー)。取り込み対象ではなく、参考情報として呼び出し側に返す。
        /// </summary>
        public List<string> AlreadyOwnedFiles { get; init; } = new();
    }

    /// <summary>
    /// iwara カスタムタグの無い mp4 を、DB 上の動画とファイルパスから照合する。
    /// 外部ツールで手動DL済みのファイルをタグ無しのまま「既にDL済み」と認識させるための下準備。
    /// ファイル I/O は行わない (呼び出し側が対象ファイルパスと動画一覧を渡す) 純粋ロジック。
    ///
    /// 照合は2段構え:
    ///   1. パス中のトークン (ファイル名 / フォルダ名を非英数字で分割したもの) が
    ///      既知の videoId と完全一致するか (最優先・ほぼ確実)
    ///   2. 1で決着しなければファイル名とタイトル文字列の一致度で判定。このとき
    ///      祖先フォルダ名が既知の作者名と一致すれば、その作者の動画だけに絞り込んでから判定する
    ///      (同名タイトルが複数作者に存在する場合の曖昧性を減らすため)。絞り込んで0件なら
    ///      絞り込み前の全候補にフォールバックする。
    /// </summary>
    public static class FilenameMatcher
    {
        /// <summary>
        /// Substring / Prefix 照合を許可する最短文字数。短い方の文字列がこれ未満だと、
        /// 無関係な作品のタイトルにもたやすく部分一致してしまう
        /// (例: タイトルが "AB" のファイルは "AB something..." という無関係な動画にも一致する)。
        /// Exact はこの下限の対象外 (両者が完全一致するなら短くても意味のある一致)。
        /// </summary>
        private const int MinFuzzyMatchLength = 20;

        private static readonly Regex NonAlnum = new("[^A-Za-z0-9]+", RegexOptions.Compiled);

        /// <summary>
        /// untaggedFilePaths の各ファイルを allVideos と照合する。
        /// 曖昧な組み合わせ (1ファイルに複数候補 / 1候補に複数ファイル) は除外せず、
        /// Ambiguous=true として結果に含める (呼び出し側の確認UIで提示するため)。
        /// </summary>
        /// <param name="allVideos">DB上の全動画。videoId 直接一致の照合先として使う (DL済みも含む)</param>
        /// <param name="scanRoot">ユーザーが選択したスキャン対象フォルダ。祖先フォルダ名の走査はここで止める</param>
        /// <param name="templateHints">
        /// PathTemplate.Extract で事前に抽出した、ファイルパスごとのヒント (任意)。
        /// 値がある項目は、そのファイルに限り自動推測 (ファイル名まるごと/祖先フォルダ走査) より優先する。
        /// 抽出できなかったファイル (辞書にキーが無い) は通常通りの自動推測にフォールバックする。
        /// </param>
        public static FilenameMatchResult Match(
            IEnumerable<string> untaggedFilePaths, IEnumerable<VideoInfo> allVideos, string scanRoot,
            IReadOnlyDictionary<string, PathTemplate.ExtractResult>? templateHints = null)
        {
            var files = untaggedFilePaths.ToList();
            var allVideosList = allVideos.ToList();
            var result = new FilenameMatchResult();

            // videoId は大文字小文字を区別する不透明な文字列なので Ordinal 比較。
            // 同じ videoId が重複行として複数存在することは無い想定だが、保険で First() を取る。
            var idIndex = allVideosList
                .Where(v => !string.IsNullOrEmpty(v.VideoId))
                .GroupBy(v => v.VideoId, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            // タイトル照合の候補プールは「ローカルに実ファイルが無い動画」のみ
            // (Completed でもファイルが欠損してれば対象、Failed/Skipped/Pending/Paused 等も含む)
            var candidates = allVideosList.Where(v => !v.LocalFileExists).ToList();

            // Phase 0: videoId トークンの直接一致 (最優先)
            var remainingFiles = new List<string>();
            foreach (var f in files)
            {
                VideoInfo? idHit = null;
                PathTemplate.ExtractResult? idHintEntry = null;
                templateHints?.TryGetValue(f, out idHintEntry);

                if (!string.IsNullOrEmpty(idHintEntry?.IdValue))
                    idIndex.TryGetValue(idHintEntry!.IdValue!, out idHit);
                else
                    idHit = FindIdToken(f, scanRoot, idIndex);

                if (idHit == null)
                {
                    remainingFiles.Add(f);
                    continue;
                }

                if (idHit.LocalFileExists)
                {
                    // 既に別の場所にDL済みの動画と同じID → このファイルは重複コピー。
                    // 無関係な動画への誤マッチを避けるため、タイトル照合にはフォールスルーさせない。
                    result.AlreadyOwnedFiles.Add(f);
                }
                else
                {
                    result.Matches.Add(new TitleMatchCandidate
                    {
                        FilePath = f,
                        Video = idHit,
                        Tier = TitleMatchTier.Id,
                        Ambiguous = false,
                        AmbiguousReason = AmbiguousReason.None,
                    });
                }
            }

            // 作者フォルダによる絞り込み用: サニタイズ済み作者名 → その作者の候補動画一覧
            var authorLookup = candidates
                .Where(v => !string.IsNullOrEmpty(v.AuthorUsername))
                .GroupBy(v => Helpers.SanitizeFileName(v.AuthorUsername), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            // タイトルが空 (SanitizeFileName のフォールバック "untitled" 含む) の候補は
            // 何にでも一致してしまうため事前に除外
            static bool HasUsableTitle(VideoInfo v)
            {
                var t = Helpers.SanitizeFileName(v.Title ?? "");
                return !string.IsNullOrEmpty(t) && !string.Equals(t, "untitled", StringComparison.OrdinalIgnoreCase);
            }

            // Phase 1: ファイルごとに全ヒットを集める (作者フォルダの絞り込み込み)
            var hitsByFile = new Dictionary<string, List<(VideoInfo Video, TitleMatchTier Tier, string Title)>>();
            foreach (var f in remainingFiles)
            {
                PathTemplate.ExtractResult? hint = null;
                templateHints?.TryGetValue(f, out hint);

                var baseName = !string.IsNullOrEmpty(hint?.TitleValue)
                    ? hint!.TitleValue!
                    : Path.GetFileNameWithoutExtension(f);
                if (string.IsNullOrEmpty(baseName)) continue;

                var pool = candidates.Where(HasUsableTitle).ToList();
                List<VideoInfo>? hinted = null;
                if (!string.IsNullOrEmpty(hint?.ArtistValue))
                    authorLookup.TryGetValue(Helpers.SanitizeFileName(hint!.ArtistValue!), out hinted);
                hinted ??= FindAuthorHintPool(f, scanRoot, authorLookup);
                if (hinted != null)
                {
                    var narrowed = hinted.Where(HasUsableTitle).ToList();
                    if (narrowed.Count > 0) pool = narrowed; // 絞り込みが空振りなら全体プールにフォールバック
                }

                var hits = CollectTitleHits(baseName, pool);
                if (hits.Count > 0) hitsByFile[f] = hits;
            }

            // Phase 2: ファイルごとに最良候補を1つに絞る。
            //   ①最良Tier優先 → ②同Tier内はタイトルが最長のものを採用 (入れ子タイトル対策) →
            //   ③最長が複数あれば曖昧
            var pickByFile = new Dictionary<string, (VideoInfo Video, TitleMatchTier Tier, bool Ambiguous, AmbiguousReason Reason, List<VideoInfo>? Alternatives)>();
            foreach (var (file, hits) in hitsByFile)
            {
                var bestTier = hits.Min(h => h.Tier);
                var atBestTier = hits.Where(h => h.Tier == bestTier).ToList();
                var maxLen = atBestTier.Max(h => h.Title.Length);
                var longest = atBestTier
                    .Where(h => h.Title.Length == maxLen)
                    .Select(h => h.Video)
                    .Distinct()
                    .ToList();

                pickByFile[file] = longest.Count > 1
                    ? (longest[0], bestTier, true, AmbiguousReason.MultipleCandidatesForFile, longest)
                    : (longest[0], bestTier, false, AmbiguousReason.None, null);
            }

            // Phase 3: 同じ候補が複数ファイルに選ばれていないか (逆方向の曖昧性)。
            // 作者フォルダでプールを絞り込んでいても、この判定は全ファイルの結果に対して
            // 一度だけグローバルに行う (別々の作者フォルダに同じ動画が入っていた場合を拾うため)。
            // グルーピングキーは DB の Id (int) ではなく VideoId (string) を使う。
            // API から取得しただけでまだ DB 未登録の候補は Id=0 のままなので、
            // Id でグルーピングすると無関係な候補同士が同一グループに潰れて誤って曖昧化する。
            var filesByVideoId = pickByFile
                .GroupBy(kv => kv.Value.Video.VideoId, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Select(kv => kv.Key).ToList());

            foreach (var (file, pick) in pickByFile)
            {
                bool videoAmbiguous = filesByVideoId[pick.Video.VideoId].Count > 1;
                result.Matches.Add(new TitleMatchCandidate
                {
                    FilePath = file,
                    Video = pick.Video,
                    Tier = pick.Tier,
                    Ambiguous = pick.Ambiguous || videoAmbiguous,
                    AmbiguousReason = pick.Ambiguous
                        ? pick.Reason
                        : (videoAmbiguous ? AmbiguousReason.MultipleFilesForCandidate : AmbiguousReason.None),
                    AlternativeCandidates = pick.Alternatives,
                });
            }
            return result;
        }

        private static List<(VideoInfo Video, TitleMatchTier Tier, string Title)> CollectTitleHits(
            string baseName, List<VideoInfo> pool)
        {
            var hits = new List<(VideoInfo Video, TitleMatchTier Tier, string Title)>();
            foreach (var v in pool)
            {
                var title = Helpers.SanitizeFileName(v.Title ?? "");
                if (string.Equals(baseName, title, StringComparison.OrdinalIgnoreCase))
                    hits.Add((v, TitleMatchTier.Exact, title));
                else if (Math.Min(baseName.Length, title.Length) >= MinFuzzyMatchLength
                         && (baseName.Contains(title, StringComparison.OrdinalIgnoreCase)
                             || title.Contains(baseName, StringComparison.OrdinalIgnoreCase)))
                    hits.Add((v, TitleMatchTier.Substring, title));
                else if (IsPrefixMatch(baseName, title))
                    hits.Add((v, TitleMatchTier.Prefix, title));
            }
            return hits;
        }

        /// <summary>
        /// ファイル名 + 祖先フォルダ名 (scanRoot まで) を非英数字で分割したトークンの中に、
        /// 既知の videoId と完全一致するものが無いか探す。
        /// </summary>
        private static VideoInfo? FindIdToken(string filePath, string scanRoot, Dictionary<string, VideoInfo> idIndex)
        {
            foreach (var segment in PathSegments(filePath, scanRoot))
            {
                foreach (var token in NonAlnum.Split(segment))
                {
                    if (token.Length == 0) continue;
                    if (idIndex.TryGetValue(token, out var v)) return v;
                }
            }
            return null;
        }

        /// <summary>
        /// 祖先フォルダ名 (scanRoot まで、近い順) のいずれかが既知の作者名 (サニタイズ後) と
        /// 一致すれば、その作者の候補一覧を返す。見つからなければ null。
        /// </summary>
        private static List<VideoInfo>? FindAuthorHintPool(
            string filePath, string scanRoot, Dictionary<string, List<VideoInfo>> authorLookup)
        {
            foreach (var folder in AncestorFolderNames(filePath, scanRoot))
            {
                var sanitized = Helpers.SanitizeFileName(folder);
                if (authorLookup.TryGetValue(sanitized, out var list)) return list;
            }
            return null;
        }

        /// <summary>ファイル名 (拡張子無し) + 祖先フォルダ名 (scanRoot 含む、近い順)</summary>
        private static IEnumerable<string> PathSegments(string filePath, string scanRoot)
        {
            var baseName = Path.GetFileNameWithoutExtension(filePath);
            if (!string.IsNullOrEmpty(baseName)) yield return baseName;
            foreach (var folder in AncestorFolderNames(filePath, scanRoot)) yield return folder;
        }

        /// <summary>
        /// filePath の直近の親フォルダから scanRoot (自身を含む) まで、近い順にフォルダ名を返す。
        /// scanRoot の外に出ることはない (ユーザーが選んだ範囲外のフォルダ名を誤って拾わないため)。
        /// </summary>
        private static IEnumerable<string> AncestorFolderNames(string filePath, string scanRoot)
        {
            string scanRootFull;
            string? dir;
            try
            {
                scanRootFull = Path.GetFullPath(scanRoot).TrimEnd('\\', '/');
                dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
            }
            catch { yield break; }

            while (!string.IsNullOrEmpty(dir))
            {
                yield return Path.GetFileName(dir) is { Length: > 0 } name ? name : dir;

                if (string.Equals(dir.TrimEnd('\\', '/'), scanRootFull, StringComparison.OrdinalIgnoreCase))
                    yield break;

                var parent = Path.GetDirectoryName(dir);
                if (string.IsNullOrEmpty(parent) || parent == dir) yield break; // ドライブ直下まで到達
                dir = parent;
            }
        }

        /// <summary>
        /// 完全な包含関係 (StartsWith) は Substring 側で既に判定済みなので、ここに来る時点で
        /// 「どちらも他方を丸ごとは含まない」ことが確定している。それでも共通の先頭部分が
        /// 一定長あれば、別ツールがそれぞれ違う位置でタイトルを切り詰めた (200字切り詰め等)
        /// ケースを拾える。共通接頭辞の長さだけを見る (末尾の食い違いは許容)。
        /// </summary>
        private static bool IsPrefixMatch(string a, string b)
        {
            int max = Math.Min(a.Length, b.Length);
            int common = 0;
            while (common < max
                   && char.ToUpperInvariant(a[common]) == char.ToUpperInvariant(b[common]))
                common++;
            return common >= MinFuzzyMatchLength;
        }
    }
}
