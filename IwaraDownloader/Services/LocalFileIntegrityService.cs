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
