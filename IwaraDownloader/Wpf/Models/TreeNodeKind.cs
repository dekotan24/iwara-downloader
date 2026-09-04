namespace IwaraDownloader.Wpf.Models
{
    /// <summary>
    /// チャンネルツリーの固定ノード種別。旧WinForms版MainFormのNODE_*定数に対応。
    /// </summary>
    public enum TreeNodeKind
    {
        AllVideos,
        Favorites,
        AllDownloads,
        NotDownloaded,
        Downloaded,
        Skipped,
        FailedVideos,
        Excluded,
        Channel,
    }
}
