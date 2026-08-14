namespace IwaraDownloader.Models
{
    /// <summary>
    /// チャンネルツリー表示用の件数集計 (DatabaseService.GetVideoTreeCounts の戻り値)
    /// </summary>
    public class VideoTreeCounts
    {
        public int Total { get; set; }
        public int Completed { get; set; }
        public int Failed { get; set; }
        public int Skipped { get; set; }
        public int NotDownloaded { get; set; }
        public int Favorite { get; set; }
        public int SingleVideos { get; set; }

        /// <summary>SubscribedUserId をキーにしたチャンネル別集計</summary>
        public Dictionary<int, ChannelVideoCounts> ByChannel { get; set; } = new();
    }

    /// <summary>チャンネル1件分の件数集計</summary>
    public class ChannelVideoCounts
    {
        public int Total { get; set; }
        public int Completed { get; set; }
        public int Downloading { get; set; }
        public int Pending { get; set; }
        public int Paused { get; set; }
    }
}
