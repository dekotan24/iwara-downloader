namespace IwaraDownloader.Models
{
    /// <summary>
    /// ダウンロードタスク（進捗管理用）
    /// </summary>
    public class DownloadTask
    {
        /// <summary>タスクID（GUID）</summary>
        public string TaskId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>動画情報</summary>
        public VideoInfo Video { get; set; } = new();

        /// <summary>ステータス</summary>
        public DownloadStatus Status { get; set; } = DownloadStatus.Pending;

        /// <summary>進捗（0-100）</summary>
        public double Progress { get; set; }

        /// <summary>ダウンロード速度（bytes/sec）</summary>
        public long DownloadSpeed { get; set; }

        /// <summary>推定残り時間（秒）</summary>
        public int? EstimatedTimeRemaining { get; set; }

        /// <summary>開始日時</summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>完了日時</summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>エラーメッセージ</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>購読DLかどうか</summary>
        public bool IsSubscriptionDownload { get; set; }

        /// <summary>購読ユーザー（カスタム保存先用）</summary>
        public SubscribedUser? SubscribedUser { get; set; }

        /// <summary>画質設定</summary>
        public VideoQuality Quality { get; set; } = VideoQuality.Source;

        /// <summary>キャンセルトークン</summary>
        public CancellationTokenSource? CancellationTokenSource { get; set; }

        /// <summary>
        /// ダウンロード速度を表示用にフォーマット
        /// </summary>
        public string SpeedFormatted
        {
            get
            {
                if (DownloadSpeed <= 0) return "-";
                string[] sizes = { "B/s", "KB/s", "MB/s", "GB/s" };
                int order = 0;
                double speed = DownloadSpeed;
                while (speed >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    speed /= 1024;
                }
                return $"{speed:0.##} {sizes[order]}";
            }
        }

        /// <summary>
        /// 残り時間を表示用にフォーマット
        /// </summary>
        public string EtaFormatted
        {
            get
            {
                if (!EstimatedTimeRemaining.HasValue || EstimatedTimeRemaining.Value <= 0)
                    return "-";

                var ts = TimeSpan.FromSeconds(EstimatedTimeRemaining.Value);
                if (ts.Hours > 0)
                    return $"{ts.Hours}時間{ts.Minutes}分";
                if (ts.Minutes > 0)
                    return $"{ts.Minutes}分{ts.Seconds}秒";
                return $"{ts.Seconds}秒";
            }
        }

        /// <summary>
        /// 進捗を表示用にフォーマット
        /// </summary>
        public string ProgressFormatted => $"{Progress:F1}%";

        /// <summary>
        /// キャンセル
        /// </summary>
        public void Cancel()
        {
            CancellationTokenSource?.Cancel();
            Status = DownloadStatus.Paused;
        }
    }
}
