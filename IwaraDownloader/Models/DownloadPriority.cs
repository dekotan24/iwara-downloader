namespace IwaraDownloader.Models
{
    /// <summary>
    /// DLキューの優先度。数値が大きいほど先に処理される。
    /// </summary>
    public enum DownloadPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Highest = 3,
    }
}
