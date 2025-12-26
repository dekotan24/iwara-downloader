namespace IwaraDownloader.Models
{
    /// <summary>
    /// ダウンロードステータス
    /// </summary>
    public enum DownloadStatus
    {
        /// <summary>待機中</summary>
        Pending,
        /// <summary>ダウンロード中</summary>
        Downloading,
        /// <summary>完了</summary>
        Completed,
        /// <summary>失敗</summary>
        Failed,
        /// <summary>スキップ（既存）</summary>
        Skipped,
        /// <summary>一時停止</summary>
        Paused
    }

    /// <summary>
    /// 画質設定
    /// </summary>
    public enum VideoQuality
    {
        Source,
        Quality1080p,
        Quality720p,
        Quality540p,
        Quality360p
    }
}
