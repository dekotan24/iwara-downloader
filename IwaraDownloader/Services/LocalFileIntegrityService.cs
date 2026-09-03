using IwaraDownloader.Models;

namespace IwaraDownloader.Services
{
    /// <summary>
    /// DBが保持するローカルファイルの整合性を確認する純粋な判定部分。
    /// 実際のファイル再マップや再ダウンロード投入は、UI/DownloadManager側で行う。
    /// </summary>
    public static class LocalFileIntegrityService
    {
        public enum IssueKind
        {
            /// <summary>完了扱いだが、DBの保存先パスが空。</summary>
            MissingPath,

            /// <summary>DBの保存先パスはあるが、実ファイルが存在しない。</summary>
            MissingFile,
        }

        public sealed class Issue
        {
            public VideoInfo Video { get; init; } = new();
            public IssueKind Kind { get; init; }
        }

        /// <summary>単一ルート(ドライブ/UNC共有)の到達性判定を差し替えるためのフック。</summary>
        public delegate bool RootReachableProbe(string root);

        /// <summary>1回のスキャン結果。到達不能なルートと打ち切りの有無を呼び出し側へ伝える。</summary>
        public sealed class ScanResult
        {
            public List<Issue> Issues { get; init; } = new();

            /// <summary>存在しない/応答しないドライブ・共有のルート。</summary>
            public IReadOnlyList<string> UnreachableRoots { get; init; } = Array.Empty<string>();

            /// <summary>到達不能なルート上にあるため判定対象から外した動画の件数。</summary>
            public int SkippedOnUnreachableRoots { get; init; }

            /// <summary>打ち切り前の実際の欠損件数。</summary>
            public int TotalIssueCount { get; init; }

            public bool Truncated => Issues.Count < TotalIssueCount;
        }

        /// <summary>
        /// 一覧に載せる欠損の上限。外付けドライブが未接続だと数万件が一度に「欠損」判定になり、
        /// グリッドへの流し込みと、そこからの一括再ダウンロードが事故になるため上限を設ける。
        /// </summary>
        public const int MaxReportedIssues = 1000;

        /// <summary>
        /// 欠損を抽出する。到達できないルート(未接続の外付けドライブ等)にあるファイルは
        /// 「消えた」のではなく「今は見えない」だけなので、欠損として数えず呼び出し側へ報告する。
        /// </summary>
        public static ScanResult Scan(
            IEnumerable<VideoInfo> videos,
            RootReachableProbe? rootReachable = null,
            CancellationToken cancellationToken = default)
        {
            rootReachable ??= DefaultRootReachable;
            var videoList = videos as IReadOnlyList<VideoInfo> ?? videos.ToList();

            var rootState = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var unreachableRoots = new List<string>();
            var target = new List<VideoInfo>(videoList.Count);
            var skipped = 0;

            foreach (var video in videoList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var root = GetRoot(video.LocalFilePath);
                if (root.Length == 0)
                {
                    target.Add(video);
                    continue;
                }

                if (!rootState.TryGetValue(root, out var reachable))
                {
                    reachable = rootReachable(root);
                    rootState[root] = reachable;
                    if (!reachable) unreachableRoots.Add(root);
                }

                if (reachable) target.Add(video);
                else skipped++;
            }

            var issues = FindIssues(target, cancellationToken);
            return new ScanResult
            {
                Issues = issues.Count > MaxReportedIssues
                    ? issues.GetRange(0, MaxReportedIssues)
                    : issues,
                UnreachableRoots = unreachableRoots,
                SkippedOnUnreachableRoots = skipped,
                TotalIssueCount = issues.Count,
            };
        }

        private static string GetRoot(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            try { return Path.GetPathRoot(path) ?? ""; }
            catch { return ""; }
        }

        private static bool DefaultRootReachable(string root)
        {
            try { return Directory.Exists(root); }
            catch { return false; }
        }

        /// <summary>
        /// 「DBにローカル実体があるはずなのに参照できない」動画を抽出する。
        /// Completed + 空パスも、DB上は完了済みなのに実体を辿れないため対象に含める。
        /// Pending/Paused/Failedで空パスの動画は通常の未ダウンロード状態なので対象外。
        /// </summary>
        public static List<Issue> FindIssues(
            IEnumerable<VideoInfo> videos, CancellationToken cancellationToken = default)
        {
            var issues = new List<Issue>();
            foreach (var video in videos)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Issue? issue;
                if (string.IsNullOrWhiteSpace(video.LocalFilePath))
                {
                    issue = video.Status == DownloadStatus.Completed
                        ? new Issue { Video = video, Kind = IssueKind.MissingPath }
                        : null;
                }
                else
                {
                    issue = File.Exists(video.LocalFilePath)
                        ? null
                        : new Issue { Video = video, Kind = IssueKind.MissingFile };
                }

                if (issue != null) issues.Add(issue);
            }

            issues.Sort((left, right) =>
            {
                var titleOrder = StringComparer.OrdinalIgnoreCase.Compare(
                    left.Video.Title, right.Video.Title);
                return titleOrder != 0
                    ? titleOrder
                    : left.Video.Id.CompareTo(right.Video.Id);
            });
            return issues;
        }

        /// <summary>画面表示後に状態が変わっていないか再確認するための軽量判定。</summary>
        public static bool IsIssue(VideoInfo video)
            => string.IsNullOrWhiteSpace(video.LocalFilePath)
                ? video.Status == DownloadStatus.Completed
                : !File.Exists(video.LocalFilePath);
    }
}
