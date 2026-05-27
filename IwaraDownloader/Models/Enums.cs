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
        /// <summary>スキップ(既存)</summary>
        Skipped,
        /// <summary>一時停止</summary>
        Paused,
        /// <summary>タグ書き込み中 (mp4 メタデータ更新)</summary>
        WritingTags
    }

    /// <summary>
    /// 画質設定 (iwara のダウンロードUIに準拠: Source / 540 / 360)
    /// 数値は旧 enum (Source=0, 1080p=1, 720p=2, 540p=3, 360p=4) と互換性を保つため
    /// 明示的に指定する。既存 settings.json の数値がそのまま読み込める。
    /// </summary>
    public enum VideoQuality
    {
        Source = 0,
        Quality540p = 3,
        Quality360p = 4,
    }
}
